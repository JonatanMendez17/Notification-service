using Serilog;
using Serilog.Events;

namespace Notification.Api.Infrastructure.Logging;

public static class SerilogSetup
{
    public static void Configure(IConfiguration configuration, string projectName, string filePrefix)
    {
        var logPath = configuration["Logging:Path"];
        var logDirectory = string.IsNullOrWhiteSpace(logPath)
            ? Path.Combine(AppContext.BaseDirectory, "log", projectName)
            : Path.IsPathRooted(logPath) ? logPath : Path.Combine(AppContext.BaseDirectory, logPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            // El logging default de HttpClient loguea la URL completa de cada request, incluido
            // el token del bot de Telegram en texto plano. Se apaga: nuestro propio código ya
            // menciona cada llamada (éxito/fallo) sin exponer la URL.
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, $"{filePrefix}_.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();
    }
}
