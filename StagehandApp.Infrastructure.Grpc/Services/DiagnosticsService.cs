namespace StagehandApp.Server.Grpc.Services;

public class DiagnosticsService
{
    public static object GetDiagnosticsInfo()
    {
        var os = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        var architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
        var framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(
            System.Runtime.InteropServices.OSPlatform.Windows);

        string mediaServiceType;
        string platformSupport;

        mediaServiceType = "WindowsMediaService (Full Windows integration)";
        platformSupport = "Full Windows media control";

        return new
        {
            OS = os,
            Architecture = architecture,
            IsWindows = isWindows,
            TargetFramework = "net9.0",
            RuntimeFramework = framework,
            MediaServiceType = mediaServiceType,
            PlatformSupport = platformSupport,
            Timestamp = DateTime.UtcNow,
            CompilationFlags = GetCompilationFlags()
        };
    }

    private static string GetCompilationFlags()
    {
        var flags = new List<string>
        {
            "WINDOWS",
            "USE_WINDOWS_MEDIA"
        };
        return string.Join(", ", flags);
    }
}