using Notification.Api.Providers;
using Notification.Api.Services;
using Notification.Api.Settings;

var builder = WebApplication.CreateBuilder(args);

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
app.Run();
