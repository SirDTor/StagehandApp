using Avalonia.Media.Imaging;
using StagehandApp.Core;
using Windows.Media.Control;
using Windows.Storage.Streams;
using System.Timers;
using Timer = System.Timers.Timer;

namespace StagehandApp.Infrastructure.Windows
{
    public class WindowsMediaService : IMediaService, IDisposable
    {
        private GlobalSystemMediaTransportControlsSessionManager? _manager;
        private GlobalSystemMediaTransportControlsSession? _session;
        private readonly Timer _checkTimer;

        // Переменные для отслеживания предыдущего состояния
        private string _lastTitle = "";
        private string _lastArtist = "";
        private string _lastAlbum = "";
        private MediaStatus _lastStatus = MediaStatus.Unknown;
        private bool _hasActiveSession = false;

        public event Action<string, string, string, byte[]?>? OnTrackChanged;
        public MediaStatus CurrentStatus { get; private set; }

        public WindowsMediaService()
        {
            _checkTimer = new Timer(1000); // Проверка каждую секунду
            _checkTimer.Elapsed += async (s, e) => await CheckMediaAsync();
        }

        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("Инициализация WindowsMediaService...");

                _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

                // Подписываемся на события
                _manager.SessionsChanged += OnSessionsChanged;

                // Запускаем таймер
                _checkTimer.Start();

                Console.WriteLine("WindowsMediaService инициализирован");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка инициализации: {ex.Message}");
            }
        }

        private async void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            Console.WriteLine("Обнаружено изменение в списке сессий");
            await CheckMediaAsync();
        }

        private async Task CheckMediaAsync()
        {
            try
            {
                // Всегда получаем текущую сессию
                var currentSession = _manager?.GetCurrentSession();

                // Если сессия изменилась
                if (currentSession != _session)
                {
                    _session = currentSession;
                    if (_session != null)
                    {
                        Console.WriteLine($"Обновлена сессия: {_session.SourceAppUserModelId}");
                        // Сбрасываем предыдущее состояние чтобы гарантировать обновление
                        _lastTitle = "";
                        _lastArtist = "";
                        _lastAlbum = "";
                        _lastStatus = MediaStatus.Unknown;
                    }
                }

                if (_session == null)
                {
                    // Если сессия пропала, но ранее была
                    if (_hasActiveSession)
                    {
                        Console.WriteLine("Медиасессия завершена");
                        _hasActiveSession = false;
                        OnTrackChanged?.Invoke("Нет медиа", "Запустите плеер", "", null);
                    }
                    return;
                }

                _hasActiveSession = true;

                var mediaInfo = await _session.TryGetMediaPropertiesAsync();
                var playbackInfo = _session.GetPlaybackInfo();

                var title = mediaInfo?.Title ?? "";
                var artist = mediaInfo?.Artist ?? "";
                var album = mediaInfo?.AlbumTitle ?? "";
                var status = playbackInfo?.PlaybackStatus switch
                {
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => MediaStatus.Playing,
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => MediaStatus.Paused,
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => MediaStatus.Stopped,
                    _ => MediaStatus.Unknown
                };

                CurrentStatus = status;

                // Проверяем, изменились ли данные
                bool hasChanged = title != _lastTitle ||
                                 artist != _lastArtist ||
                                 album != _lastAlbum ||
                                 status != _lastStatus;

                if (hasChanged)
                {
                    _lastTitle = title;
                    _lastArtist = artist;
                    _lastAlbum = album;
                    _lastStatus = status;

                    byte[]? albumArtData = null;
                    if (mediaInfo?.Thumbnail != null)
                    {
                        try
                        {
                            using var stream = await mediaInfo.Thumbnail.OpenReadAsync();
                            using var memoryStream = new System.IO.MemoryStream();
                            await stream.AsStreamForRead().CopyToAsync(memoryStream);
                            albumArtData = memoryStream.ToArray();
                            Console.WriteLine($"Обложка загружена: {albumArtData.Length} bytes");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Ошибка загрузки обложки: {ex.Message}");
                        }
                    }

                    Console.WriteLine($"Трек изменился: {artist} - {title} | Статус: {status}");

                    // Отправляем событие только если данные изменились
                    OnTrackChanged?.Invoke(
                        string.IsNullOrEmpty(title) ? "Без названия" : title,
                        string.IsNullOrEmpty(artist) ? "Неизвестный артист" : artist,
                        string.IsNullOrEmpty(album) ? "" : album,
                        albumArtData
                    );
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка проверки медиа: {ex.Message}");
            }
        }

        public async Task PlayAsync()
        {
            var session = _manager?.GetCurrentSession();
            if (session != null)
            {
                await session.TryPlayAsync();
                Console.WriteLine("Команда: Play");
            }
        }

        public async Task PauseAsync()
        {
            var session = _manager?.GetCurrentSession();
            if (session != null)
            {
                await session.TryPauseAsync();
                Console.WriteLine("Команда: Pause");
            }
        }

        public async Task NextAsync()
        {
            var session = _manager?.GetCurrentSession();
            if (session != null)
            {
                await session.TrySkipNextAsync();
                Console.WriteLine("Команда: Next");
            }
        }

        public async Task PreviousAsync()
        {
            var session = _manager?.GetCurrentSession();
            if (session != null)
            {
                await session.TrySkipPreviousAsync();
                Console.WriteLine("Команда: Previous");
            }
        }

        public void Dispose()
        {
            _checkTimer?.Stop();
            _checkTimer?.Dispose();
        }
    }
}