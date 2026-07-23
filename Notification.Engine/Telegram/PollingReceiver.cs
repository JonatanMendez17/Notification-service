using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Notification.Engine.Settings;

namespace Notification.Engine.Telegram;

// Único componente que le habla a Telegram para recibir updates en dev (spec 4.5).
// En server se reemplaza por WebhookReceiver — nunca corren los dos a la vez, evita
// el problema que tenía N8N de dos consumidores compitiendo por el mismo offset.
public class PollingReceiver : BackgroundService
{
    private const string ApiBase = "https://api.telegram.org";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollingReceiver> _logger;

    // Offset en memoria: se pierde en un reinicio del proceso, lo que en el peor caso
    // reprocesa updates ya vistos (no es un problema práctico: los UPDATE de SQL son
    // idempotentes y el alta de grupo ya está protegida contra duplicados). No bloqueante
    // para el prototipo — si hace falta persistencia real, se revisita más adelante.
    private long? _lastUpdateId;

    public PollingReceiver(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramSettings> settings,
        IServiceScopeFactory scopeFactory,
        ILogger<PollingReceiver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        do
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en PollingReceiver.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var offset = _lastUpdateId is { } id ? id + 1 : 0;
        var allowedUpdates = Uri.EscapeDataString("[\"message\",\"callback_query\"]");
        var url = $"{ApiBase}/bot{_settings.Token}/getUpdates?offset={offset}&timeout=0&allowed_updates={allowedUpdates}";

        // Corre cada 5s — no se loguea el caso "sin updates nuevos" para no inundar la consola.
        var response = await client.GetFromJsonAsync<TelegramGetUpdatesResponse>(url, ct);
        if (response is not { Ok: true } || response.Result.Count == 0) return;

        _lastUpdateId = response.Result[^1].UpdateId;
        _logger.LogInformation("PollingReceiver: {Cantidad} update(s) de Telegram recibidos.", response.Result.Count);

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RespuestaRegistroHandler>();

        foreach (var update in response.Result)
        {
            if (update.CallbackQuery is not null)
            {
                await handler.OnCallbackQueryAsync(update.CallbackQuery, ct);
            }
            else if (update.Message is not null)
            {
                await handler.OnMessageAsync(update.Message, ct);
            }
        }
    }
}
