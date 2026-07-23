using Microsoft.Extensions.Hosting.WindowsServices;
using Notification.Engine.Data;
using Notification.Engine.Jobs;
using Notification.Engine.Services;
using Notification.Engine.Settings;
using Notification.Engine.Telegram;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    // Se corta solo el detalle por-request de ASP.NET Core (ruidoso y repetitivo).
    // Los mensajes de ciclo de vida (Application started, Now listening on, etc.)
    // quedan — confirman que el servicio arrancó bien.
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "log", "Notification.Engine", "engine_.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog();
    builder.Services.AddWindowsService();

    // Settings
    builder.Services.Configure<SqlSettings>(builder.Configuration.GetSection("Sql"));
    builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));

    // HTTP
    builder.Services.AddHttpClient();

    // Data
    builder.Services.AddScoped<ISqlDataAccess, SqlDataAccess>();
    builder.Services.AddScoped<IHitosRepository, HitosRepository>();
    builder.Services.AddScoped<IGruposRepository, GruposRepository>();

    // Telegram
    builder.Services.AddSingleton<TelegramBotClient>();
    builder.Services.AddScoped<RespuestaRegistroHandler>();

    // Services
    builder.Services.AddScoped<IEnvioDiarioFilterService, EnvioDiarioFilterService>();

    // Jobs
    builder.Services.AddHostedService<EnvioDiarioJob>();
    builder.Services.AddHostedService<ReprogramarJob>();
    builder.Services.AddHostedService<ResetMensualJob>();
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
