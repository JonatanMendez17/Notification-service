using Microsoft.Extensions.Hosting.WindowsServices;
using Notification.Engine.Data;
using Notification.Engine.Jobs;
using Notification.Engine.Services;
using Notification.Engine.Settings;
using Notification.Engine.Telegram;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
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

    // Telegram
    builder.Services.AddSingleton<TelegramBotClient>();

    // Services
    builder.Services.AddScoped<IEnvioDiarioFilterService, EnvioDiarioFilterService>();

    // Jobs
    builder.Services.AddHostedService<EnvioDiarioJob>();

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
