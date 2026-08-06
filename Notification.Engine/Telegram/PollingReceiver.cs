using System.Net.Http.Json;
using Notification.Engine.Common;

namespace Notification.Engine.Telegram;

// Único componente que le habla a Telegram para recibir updates en desarrollo.
// En PROD se reemplaza por WebhookReceiver — nunca corren los dos a la vez
public class PollingReceiver(IHttpClientFactory httpClientFactory, TelegramTokenProvider tokenProvider, IServiceScopeFactory scopeFactory, ILogger<PollingReceiver> logger) : RecurringBackgroundService(TimeSpan.FromSeconds(5), logger)
{
    private const string ApiBase = "https://api.telegram.org";

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly TelegramTokenProvider _tokenProvider = tokenProvider;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    private long? _lastUpdateId;

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var offset = _lastUpdateId is { } id ? id + 1 : 0;
        var allowedUpdates = Uri.EscapeDataString("[\"message\",\"callback_query\"]");
        var token = await _tokenProvider.ObtenerTokenAsync(ct);
        var url = $"{ApiBase}/bot{token}/getUpdates?offset={offset}&timeout=0&allowed_updates={allowedUpdates}";

        // Corre cada 5s — no se loguea el caso "sin updates nuevos" para no inundar la consola.
        var response = await client.GetFromJsonAsync<TelegramGetUpdatesResponse>(url, ct);
        if (response is not { Ok: true } || response.Result.Count == 0) return;

        Logger.LogInformation("PollingReceiver: {Cantidad} update(s) de Telegram recibidos.", response.Result.Count);

        using var scope = _scopeFactory.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<RespuestaRegistroHandler>();

        // Cada update se procesa y confirma (avance de offset) de forma independiente:
        // si uno falla (SQL transitorio, cq.Message null en mensajes viejos, etc.) no debe
        // arrastrar a los que vinieron después en el mismo lote de 5s.
        foreach (var update in response.Result)
        {
            try
            {
                if (update.CallbackQuery is not null)
                {
                    await handler.ProcesarCallbackAsync(update.CallbackQuery, ct);
                }
                else if (update.Message is not null)
                {
                    await handler.ProcesarMensajeAsync(update.Message, ct);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "PollingReceiver: error procesando update {UpdateId}, se descarta sin trabar los siguientes.", update.UpdateId);
            }
            finally
            {
                _lastUpdateId = update.UpdateId;
            }
        }
    }
}
