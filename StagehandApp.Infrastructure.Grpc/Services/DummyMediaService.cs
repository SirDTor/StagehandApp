using StagehandApp.Core;

namespace StagehandApp.Server.Grpc.Services;

public class DummyMediaService : IMediaService
{
    private readonly ILogger<DummyMediaService> _logger;

    public event Action<string, string, string, byte[]?>? OnTrackChanged;
    public MediaStatus CurrentStatus => MediaStatus.Unknown;

    public DummyMediaService(ILogger<DummyMediaService> logger = null)
    {
        _logger = logger;
    }

    public Task InitializeAsync()
    {
        _logger?.LogInformation("DummyMediaService initialized (Cross-platform mode)");

        // Симулируем изменение трека для демонстрации
        SimulateTrackChange();

        return Task.CompletedTask;
    }

    public Task PlayAsync()
    {
        _logger?.LogInformation("Media command: PLAY");
        CurrentTrackStatus = "Playing";
        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        _logger?.LogInformation("Media command: PAUSE");
        CurrentTrackStatus = "Paused";
        return Task.CompletedTask;
    }

    public Task NextAsync()
    {
        _logger?.LogInformation("Media command: NEXT");
        SimulateTrackChange();
        return Task.CompletedTask;
    }

    public Task PreviousAsync()
    {
        _logger?.LogInformation("Media command: PREVIOUS");
        SimulateTrackChange();
        return Task.CompletedTask;
    }

    private string CurrentTrackStatus { get; set; } = "Stopped";

    private void SimulateTrackChange()
    {
        var demoTracks = new[]
        {
            ("Demo Song - Cross Platform", "StagehandApp", "Test Album"),
            ("Another Track", "Virtual Artist", "Demo Album"),
            ("Media Control Test", "Test Artist", "System Sounds")
        };

        var random = new Random();
        var track = demoTracks[random.Next(demoTracks.Length)];

        OnTrackChanged?.Invoke(track.Item1, track.Item2, track.Item3, null);

        _logger?.LogInformation("Simulated track change: {Artist} - {Title}", track.Item2, track.Item1);
    }
}