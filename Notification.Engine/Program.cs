using Microsoft.Extensions.Hosting.WindowsServices;
using Notification.Engine.Data;
using Notification.Engine.Jobs;
using Notification.Engine.Services;
using Notification.Engine.Settings;
using Notification.Engine.Telegram;
using Serilog;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    // Ruta de logs configurable vía "Logging:Path" en appsettings.json — si viene vacía o
    // no está seteada, cae al default de siempre (carpeta "log" junto al ejecutable).
    var logPath = builder.Configuration["Logging:Path"];
    var logDirectory = string.IsNullOrWhiteSpace(logPath)
        ? Path.Combine(AppContext.BaseDirectory, "log", "Notification.Engine")
        : Path.IsPathRooted(logPath) ? logPath : Path.Combine(AppContext.BaseDirectory, logPath);

    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Information() // Los mensajes de ciclo de vida.
        .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(logDirectory, "engine_.txt"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30)
        .CreateLogger();

    builder.Services.AddSerilog();
    builder.Services.AddWindowsService();

    // Settings
    builder.Services.Configure<SqlSettings>(builder.Configuration.GetSection("Sql"));

    // HTTP
    builder.Services.AddHttpClient();

    // Data
    builder.Services.AddScoped<ISqlDataAccess, SqlDataAccess>();
    builder.Services.AddScoped<IHitosRepository, HitosRepository>();
    builder.Services.AddScoped<IGruposRepository, GruposRepository>();

    // Telegram
    builder.Services.AddSingleton<TelegramTokenProvider>();
    builder.Services.AddSingleton<TelegramBotClient>();
    builder.Services.AddScoped<RespuestaRegistroHandler>();

    // Services
    builder.Services.AddScoped<IEnvioDiarioFilterService, EnvioDiarioFilterService>();

    // Jobs
    builder.Services.AddHostedService<EnvioDiarioJob>();
    builder.Services.AddHostedService<ReprogramarJob>();
    builder.Services.AddHostedService<ReinicioMensualJob>();
    builder.Services.AddHostedService<ActualizacionesTiempoRealJob>();

    // Receiver de updates de Telegram (dev = polling, spec 4.5)
    builder.Services.AddHostedService<PollingReceiver>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Notification.Engine terminó inesperadamente.");
}
finally
{
    Log.CloseAndFlush();
}
