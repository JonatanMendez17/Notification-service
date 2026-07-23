using Notification.Api.Providers;
using Notification.Api.Services;
using Notification.Api.Settings;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "log", "Notification.Api", "api_.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSerilog();

// Settings
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));

// HTTP
builder.Services.AddHttpClient();

// Providers — agregar aquí futuros canales (WhatsApp, Email, etc.)
builder.Services.AddScoped<INotificationProvider, TelegramProvider>();

// Services
builder.Services.AddScoped<IMensajeriaService, MensajeriaService>();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapControllers();
app.MapHealthChecks("/health");

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
