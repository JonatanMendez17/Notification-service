using Notification.Api.Authentication;
using Notification.Api.Providers;
using Notification.Api.Services;
using Notification.Api.Settings;
using Notification.Api.Telegram;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();

// Ruta de logs configurable vía "Logging:Path" en appsettings.json — si viene vacía o
// no está seteada, cae al default de siempre (carpeta "log" junto al ejecutable).
var logPath = builder.Configuration["Logging:Path"];
var logDirectory = string.IsNullOrWhiteSpace(logPath)
    ? Path.Combine(AppContext.BaseDirectory, "log", "Notification.Api")
    : Path.IsPathRooted(logPath) ? logPath : Path.Combine(AppContext.BaseDirectory, logPath);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information() // Los mensajes de ciclo de vida
    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(logDirectory, "api_.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30)
    .CreateLogger();

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
