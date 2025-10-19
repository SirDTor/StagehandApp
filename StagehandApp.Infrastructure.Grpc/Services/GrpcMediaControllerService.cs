using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using StagehandApp.Core;
using StagehandApp.Infrastructure.GrpcServer;
using MediaPlayerStatus = StagehandApp.Infrastructure.GrpcServer.MediaPlayerStatus;

namespace StagehandApp.Server.Grpc.Services;

public class GrpcMediaControllerService : Media.MediaBase
{
    private readonly IMediaService _mediaService;
    private readonly ILogger<GrpcMediaControllerService> _logger;
    private readonly List<IServerStreamWriter<MediaUpdate>> _subscribers = new();
    private readonly object _subscribersLock = new object();

    // Добавляем поле для хранения текущего трека
    private MediaUpdate _currentTrack;

    public GrpcMediaControllerService(IMediaService mediaService, ILogger<GrpcMediaControllerService> logger)
    {
        _mediaService = mediaService;
        _logger = logger;

        // Инициализируем текущий трек
        _currentTrack = new MediaUpdate
        {
            Title = "No media",
            Artist = "Start playing media",
            Album = "",
            Status = MediaPlayerStatus.Unknown
        };

        // Инициализируем медиа сервис
        _ = InitializeMediaServiceAsync();

        _logger.LogInformation("GrpcMediaControllerService created");
    }

    private async Task InitializeMediaServiceAsync()
    {
        try
        {
            _logger.LogInformation("Initializing media service...");

            // Подписываемся на события изменения медиа
            _mediaService.OnTrackChanged += OnTrackChanged;

            // Инициализируем медиа сервис
            await _mediaService.InitializeAsync();

            _logger.LogInformation("Media service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize media service");
        }
    }

    public override async Task<MediaResponse> Play(Empty request, ServerCallContext context)
    {
        try
        {
            await _mediaService.PlayAsync();
            _logger.LogInformation("Play command executed");
            return new MediaResponse { Success = true, Message = "Play command sent" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Play command");
            return new MediaResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<MediaResponse> Pause(Empty request, ServerCallContext context)
    {
        try
        {
            await _mediaService.PauseAsync();
            _logger.LogInformation("Pause command executed");
            return new MediaResponse { Success = true, Message = "Pause command sent" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Pause command");
            return new MediaResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<MediaResponse> Next(Empty request, ServerCallContext context)
    {
        try
        {
            await _mediaService.NextAsync();
            _logger.LogInformation("Next command executed");
            return new MediaResponse { Success = true, Message = "Next command sent" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Next command");
            return new MediaResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<MediaResponse> Previous(Empty request, ServerCallContext context)
    {
        try
        {
            await _mediaService.PreviousAsync();
            _logger.LogInformation("Previous command executed");
            return new MediaResponse { Success = true, Message = "Previous command sent" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Previous command");
            return new MediaResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<MediaStatusResponse> GetStatus(Empty request, ServerCallContext context)
    {
        try
        {
            return new MediaStatusResponse
            {
                Status = ConvertMediaStatus(_mediaService.CurrentStatus)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting media status");
            return new MediaStatusResponse { Status = MediaPlayerStatus.Unknown };
        }
    }

    public override async Task SubscribeToMediaChanges(Empty request,
    IServerStreamWriter<MediaUpdate> responseStream, ServerCallContext context)
    {
        var clientId = context.Peer;
        _logger.LogInformation("New client subscribed to media changes: {ClientId}", clientId);

        // Добавляем клиента в список подписчиков
        lock (_subscribersLock)
        {
            _subscribers.Add(responseStream);
        }

        try
        {
            // Сразу отправляем текущий статус при подключении
            await SendInitialStatus(responseStream);

            _logger.LogInformation("Client {ClientId} successfully subscribed", clientId);

            // Держим соединение открытым до отмены
            await Task.Delay(Timeout.Infinite, context.CancellationToken);
        }
        catch (TaskCanceledException)
        {
            _logger.LogInformation("Client {ClientId} unsubscribed from media changes", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in subscription for client {ClientId}", clientId);
        }
        finally
        {
            // Удаляем клиента из списка подписчиков
            lock (_subscribersLock)
            {
                _subscribers.Remove(responseStream);
            }
            _logger.LogInformation("Client {ClientId} removed from subscribers", clientId);
        }
    }

    private async Task SendInitialStatus(IServerStreamWriter<MediaUpdate> responseStream)
    {
        try
        {
            var initialUpdate = new MediaUpdate
            {
                Title = "No media",
                Artist = "Start playing media",
                Album = "",
                Status = MediaPlayerStatus.Unknown
            };

            await responseStream.WriteAsync(initialUpdate);
            _logger.LogDebug("Initial status sent to client");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send initial status to client");
        }
    }

    private async void OnTrackChanged(string title, string artist, string album, byte[]? art)
    {
        _logger.LogInformation("Media changed: {Artist} - {Title}", artist, title);

        // Обновляем текущий трек
        var update = new MediaUpdate
        {
            Title = title ?? "Unknown",
            Artist = artist ?? "Unknown",
            Album = album ?? "Unknown",
            Status = ConvertMediaStatus(_mediaService.CurrentStatus)
        };

        if (art != null && art.Length > 0)
        {
            update.AlbumArt = Google.Protobuf.ByteString.CopyFrom(art);
            _logger.LogDebug("Album art included: {Bytes} bytes", art.Length);
        }

        // Сохраняем текущий трек
        _currentTrack = update;

        // Отправляем обновление всем подключенным клиентам
        await SendUpdateToAllSubscribers(update);
    }

    private async Task SendUpdateToAllSubscribers(MediaUpdate update)
    {
        List<IServerStreamWriter<MediaUpdate>> currentSubscribers;

        lock (_subscribersLock)
        {
            currentSubscribers = new List<IServerStreamWriter<MediaUpdate>>(_subscribers);
        }

        if (currentSubscribers.Count == 0)
        {
            _logger.LogDebug("No subscribers to send update to");
            return;
        }

        _logger.LogDebug("Sending update to {SubscriberCount} subscribers", currentSubscribers.Count);

        var sendTasks = currentSubscribers.Select(async subscriber =>
        {
            try
            {
                await subscriber.WriteAsync(update);
                _logger.LogTrace("Update sent successfully to subscriber");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send update to subscriber");
                // Удаляем нерабочего подписчика
                lock (_subscribersLock)
                {
                    _subscribers.Remove(subscriber);
                }
            }
        });

        await Task.WhenAll(sendTasks);
    }

    private MediaPlayerStatus ConvertMediaStatus(MediaStatus status)
    {
        return status switch
        {
            MediaStatus.Playing => MediaPlayerStatus.Playing,
            MediaStatus.Paused => MediaPlayerStatus.Paused,
            MediaStatus.Stopped => MediaPlayerStatus.Stopped,
            _ => MediaPlayerStatus.Unknown
        };
    }
}