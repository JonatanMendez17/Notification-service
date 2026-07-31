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
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logDirectory, $"{filePrefix}_.txt"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();
    }
}
