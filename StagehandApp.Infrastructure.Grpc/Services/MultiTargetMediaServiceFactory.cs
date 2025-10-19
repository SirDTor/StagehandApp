using StagehandApp.Core;

namespace StagehandApp.Server.Grpc.Services;

public class MultiTargetMediaServiceFactory : IMediaServiceFactory
{
    private readonly ILogger<MultiTargetMediaServiceFactory> _logger;

    public MultiTargetMediaServiceFactory(ILogger<MultiTargetMediaServiceFactory> logger)
    {
        _logger = logger;
    }

    public IMediaService CreateMediaService()
    {
        var os = System.OperatingSystem.IsWindows();
        var framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

        _logger.LogInformation("Creating media service. Windows: {OS}, Framework: {Framework}", os, framework);

        _logger.LogInformation("Windows media service available - using WindowsMediaService");
        return CreateWindowsMediaService();
    }

    private IMediaService CreateWindowsMediaService()
    {
        try
        {
            _logger.LogInformation("Initializing WindowsMediaService...");

            var service = new global::StagehandApp.Infrastructure.Windows.WindowsMediaService();

            _logger.LogInformation("WindowsMediaService created successfully");
            return service;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create WindowsMediaService, falling back to DummyService");
            return CreateDummyMediaService();
        }
    }

    private IMediaService CreateDummyMediaService()
    {
        return new DummyMediaService();
    }
}