using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Notification.Engine.Common;
using Notification.Engine.Settings;

namespace Notification.Engine.Telegram;

// Único componente que le habla a Telegram para recibir updates en desarrollo.
// En PROD se reemplaza por WebhookReceiver — nunca corren los dos a la vez
public class PollingReceiver : PeriodicBackgroundService
{
    private const string ApiBase = "https://api.telegram.org";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;

    // El offset se guarda solo en memoria. Tras un reinicio pueden reprocesarse
    // algunos updates, pero es seguro porque las operaciones evitan duplicados.

    private long? _lastUpdateId;

    public PollingReceiver(
        IHttpClientFactory httpClientFactory,
        IOptions<TelegramSettings> settings,
        IServiceScopeFactory scopeFactory,
        ILogger<PollingReceiver> logger)
        : base(TimeSpan.FromSeconds(5), logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
    }

    protected override async Task EjecutarAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var offset = _lastUpdateId is { } id ? id + 1 : 0;
        var allowedUpdates = Uri.EscapeDataString("[\"message\",\"callback_query\"]");
        var url = $"{ApiBase}/bot{_settings.Token}/getUpdates?offset={offset}&timeout=0&allowed_updates={allowedUpdates}";

        // Corre cada 5s — no se loguea el caso "sin updates nuevos" para no inundar la consola.
        var response = await client.GetFromJsonAsync<TelegramGetUpdatesResponse>(url, ct);
        if (response is not { Ok: true } || response.Result.Count == 0) return;

        _lastUpdateId = response.Result[^1].UpdateId;
        Logger.LogInformation("PollingReceiver: {Cantidad} update(s) de Telegram recibidos.", response.Result.Count);

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
