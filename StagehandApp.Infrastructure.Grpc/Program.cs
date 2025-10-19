using StagehandApp.Core;
using StagehandApp.Infrastructure.Windows;
using StagehandApp.Server.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddDebug();
});

builder.Services.AddSingleton<IMediaService, WindowsMediaService>();

var app = builder.Build();

// Диагностический endpoint
app.MapGet("/diagnostics", () => DiagnosticsService.GetDiagnosticsInfo());

app.MapGet("/health", () => new { status = "healthy", timestamp = DateTime.UtcNow });

app.MapGet("/", () =>
{
    var info = DiagnosticsService.GetDiagnosticsInfo();

    // Используем динамическое обращение к свойствам
    var os = GetPropertyValue(info, "OS") ?? "Unknown";
    var targetFramework = GetPropertyValue(info, "TargetFramework") ?? "Unknown";
    var mediaServiceType = GetPropertyValue(info, "MediaServiceType") ?? "Unknown";

    return $@"StagehandApp gRPC Server is running!

Platform: {os}
Framework: {targetFramework}
Media Service: {mediaServiceType}

Endpoints:
- gRPC: /media.Media/*
- Diagnostics: /diagnostics
- Health: /health";
});

app.MapGrpcService<GrpcMediaControllerService>();

// Логируем информацию при запуске
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var diagnostics = DiagnosticsService.GetDiagnosticsInfo();
logger.LogInformation("Application started with configuration: {@Diagnostics}", diagnostics);

app.Run();

// Вспомогательный метод для получения значений свойств
static string GetPropertyValue(object obj, string propertyName)
{
    if (obj == null) return null;

    var property = obj.GetType().GetProperty(propertyName);
    return property?.GetValue(obj)?.ToString();
}