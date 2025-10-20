using Avalonia.Media.Imaging;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using ReactiveUI;
using StagehandApp.Infrastructure.GrpcClient;
using System;
using System.IO;
using System.Net.Http;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;

namespace StagehandApp.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly Media.MediaClient _grpcClient;
    private readonly GrpcChannel _channel;
    private CancellationTokenSource? _subscriptionCts;
    private Task? _subscriptionTask;

    private string _title = "No track";
    private string _artist = "No artist";
    private string _album = "No album";
    private Bitmap? _albumArt;
    private string _mediaStatus = "nothing";
    private string _serverStatus = "Disconnected";
    private bool _isConnected;

    public ReactiveCommand<Unit, Unit> PlayPauseCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> NextCommand { get; }
    public ReactiveCommand<Unit, Unit> PreviousCommand { get; }
    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }

    public MainViewModel()
    {
        // Создаем gRPC канал и клиент
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        _channel = GrpcChannel.ForAddress("https://localhost:7168", new GrpcChannelOptions
        {
            HttpHandler = handler
        });

        _grpcClient = new Media.MediaClient(_channel);

        // Инициализируем команды
        PlayPauseCommand = ReactiveCommand.CreateFromTask(PlayPauseAsync);
        NextCommand = ReactiveCommand.CreateFromTask(NextAsync);
        PreviousCommand = ReactiveCommand.CreateFromTask(PreviousAsync);
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);

        // Инициализируем подключение
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            ServerStatus = "Connecting to server...";
            _subscriptionCts = new CancellationTokenSource();

            var status = await _grpcClient.GetStatusAsync(new Empty());
            UpdateFromGrpcStatus(status);
            _subscriptionTask = StartMediaUpdatesSubscription(_subscriptionCts.Token);
            IsConnected = true;
            ServerStatus = "Connected to media server";
        }
        catch (Exception ex)
        {
            ServerStatus = $"Connection error: {ex.Message}";
            IsConnected = false;
        }
    }

    private async Task ConnectAsync()
    {
        await InitializeAsync();
    }

    private async Task StartMediaUpdatesSubscription(CancellationToken cancellationToken)
    {
        try
        {
            using var call = _grpcClient.SubscribeToMediaChanges(new Empty(),
                cancellationToken: cancellationToken);

            // Читаем сообщения из стрима
            await foreach (var mediaUpdate in call.ResponseStream.ReadAllAsync(cancellationToken))
            {
                // Обновляем UI при получении нового сообщения
                await UpdateFromGrpcUpdate(mediaUpdate);
            }
        }
        catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.Cancelled)
        {
            // Подписка отменена - это нормально
            MediaStatus = "Disconnected from server";
            IsConnected = false;
        }
        catch (Exception ex)
        {
            MediaStatus = $"Subscription error: {ex.Message}";
            IsConnected = false;
        }
    }

    private Task UpdateFromGrpcUpdate(MediaUpdate update)
    {
        return Task.Run(async () =>
        {
            try
            {
                Bitmap? albumArt = null;

                if (update.AlbumArt != null && update.AlbumArt.Length > 0)
                {
                    using var stream = new MemoryStream(update.AlbumArt.ToByteArray());
                    albumArt = new Bitmap(stream);
                }

                Title = update.Title ?? "Unknown Title";
                Artist = update.Artist ?? "Unknown Artist";
                Album = update.Album ?? "Unknown Album";
                AlbumArt = albumArt;
                //MediaStatus = update.Status.;
                MediaStatus = _grpcClient.GetStatus(new Empty()).Status.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing media update: {ex.Message}");
            }
        });
    }

    private void UpdateFromGrpcStatus(MediaStatusResponse status)
    {
        ServerStatus = $"Status: {status.Status}";
        MediaStatus = $"Status: {status.Status}";
    }

    private async Task PlayPauseAsync()
    {
        if (!IsConnected)
        {
            ServerStatus = "Not connected to server";
            return;
        }

        if (MediaStatus.Contains("pause", StringComparison.CurrentCultureIgnoreCase))
        {
            try
            {
                ServerStatus = "Sending play command...";
                var response = await _grpcClient.PlayAsync(new Empty());

                if (response.Success)
                    ServerStatus = "Playing";
                else
                    ServerStatus = $"Play failed: {response.Message}";
            }
            catch (Exception ex)
            {
                ServerStatus = $"Play error: {ex.Message}";
                IsConnected = false;
            }
        }
        else if (MediaStatus.Contains("play",StringComparison.CurrentCultureIgnoreCase))
        {
            try
            {
                ServerStatus = "Sending pause command...";
                var response = await _grpcClient.PauseAsync(new Empty());

                if (response.Success)
                    ServerStatus = "Paused";
                else
                    ServerStatus = $"Pause failed: {response.Message}";
            }
            catch (Exception ex)
            {
                ServerStatus = $"Pause error: {ex.Message}";
                IsConnected = false;
            }
        }

    }

    private async Task NextAsync()
    {
        if (!IsConnected)
        {
            ServerStatus = "Not connected to server";
            return;
        }

        try
        {
            ServerStatus = "Sending next command...";
            var response = await _grpcClient.NextAsync(new Empty());

            if (response.Success)
                ServerStatus = "Skipped to next track";
            else
                ServerStatus = $"Next failed: {response.Message}";
        }
        catch (Exception ex)
        {
            ServerStatus = $"Next error: {ex.Message}";
            IsConnected = false;
        }
    }

    private async Task PreviousAsync()
    {
        if (!IsConnected)
        {
            ServerStatus = "Not connected to server";
            return;
        }

        try
        {
            ServerStatus = "Sending previous command...";
            var response = await _grpcClient.PreviousAsync(new Empty());

            if (response.Success)
                ServerStatus = "Skipped to previous track";
            else
                ServerStatus = $"Previous failed: {response.Message}";
        }
        catch (Exception ex)
        {
            ServerStatus = $"Previous error: {ex.Message}";
            IsConnected = false;
        }
    }

    // Свойства
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public string Artist
    {
        get => _artist;
        set => this.RaiseAndSetIfChanged(ref _artist, value);
    }

    public string Album
    {
        get => _album;
        set => this.RaiseAndSetIfChanged(ref _album, value);
    }

    public Bitmap? AlbumArt
    {
        get => _albumArt;
        set => this.RaiseAndSetIfChanged(ref _albumArt, value);
    }

    public string MediaStatus
    {
        get => _mediaStatus;
        set => this.RaiseAndSetIfChanged(ref _mediaStatus, value);
    }

    public string ServerStatus
    {
        get => _serverStatus;
        set => this.RaiseAndSetIfChanged(ref _serverStatus, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    public void Dispose()
    {
        _channel?.Dispose();
    }
}