using Notification.Api.Authentication;
using Notification.Api.Logging;
using Notification.Api.Providers;
using Notification.Api.Services;
using Notification.Api.Settings;
using Notification.Api.Telegram;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

SerilogSetup.Configure(builder.Configuration, "Notification.Api", "api");
builder.Services.AddSerilog();

// Settings
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection("Telegram"));
builder.Services.Configure<ApiSettings>(builder.Configuration.GetSection("ApiSettings"));
builder.Services.Configure<SqlSettings>(builder.Configuration.GetSection("Sql"));

// HTTP
builder.Services.AddHttpClient();

// Telegram — token único, leído de Parametria (misma fuente que TGN Web y Notification.Engine)
builder.Services.AddSingleton<TelegramTokenProvider>();

// Providers — agregar aquí futuros canales (WhatsApp, Email, etc.)
builder.Services.AddScoped<INotificationProvider, TelegramProvider>();

// Services
builder.Services.AddScoped<IMensajeriaService, MensajeriaService>();

// Autenticación por token — se exige en todos los endpoints de controllers vía
// RequireAuthorization() más abajo, no queda a criterio de cada acción.
builder.Services.AddAuthentication(ApiKeyAuthenticationOptions.SchemeName)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationOptions.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireAuthorization();
app.MapHealthChecks("/health"); // sin auth: lo consume infra/monitoreo, no un cliente de negocio

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
