using Microsoft.Extensions.Hosting.WindowsServices;
using Notification.Engine.Data;
using Notification.Engine.Jobs;
using Notification.Engine.Logging;
using Notification.Engine.Services;
using Notification.Engine.Settings;
using Notification.Engine.Telegram;
using Serilog;

try
{
    var builder = Host.CreateApplicationBuilder(args);

    SerilogSetup.Configure(builder.Configuration, "Notification.Engine", "engine");
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
    builder.Services.AddScoped<IServicioEstadoRepository, ServicioEstadoRepository>();

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
    builder.Services.AddHostedService<HeartbeatJob>();

    // Receiver de updates de Telegram (dev = polling, spec 4.5)
    builder.Services.AddHostedService<PollingReceiver>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Notification.Engine terminó inesperadamente.");
    Environment.ExitCode = 1; // código != 0: para que Windows Recovery lo trate como falla y lo reinicie
}
finally
{
    Log.CloseAndFlush();
}
