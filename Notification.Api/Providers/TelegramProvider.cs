using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Notification.Api.Settings;

namespace Notification.Api.Providers;

public class TelegramProvider(IHttpClientFactory httpClientFactory, IOptions<TelegramSettings> settings, ILogger<TelegramProvider> logger) : INotificationProvider
{
    private const string ApiBase = "https://api.telegram.org";
    private const int MaxIntentos = 3;

    private static readonly TimeSpan[] EsperaEntreIntentos = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];
    private static readonly TimeSpan TopeEsperaRateLimit = TimeSpan.FromSeconds(5);

    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly TelegramSettings _settings = settings.Value;
    private readonly ILogger<TelegramProvider> _logger = logger;

    public string Canal => "Telegram";

    // Reintenta errores transitorios (excepcion/5xx/429); un error permanente (ej. 400) no se reintenta.
    public async Task<bool> EnviarAsync(string mensaje)
    {
        for (var intento = 1; intento <= MaxIntentos; intento++)
        {
            HttpStatusCode? statusCode = null;
            TelegramErrorResponse? errorBody = null;

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{ApiBase}/bot{_settings.Token}/sendMessage";

                var payload = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("chat_id", _settings.ChatId),
                    new KeyValuePair<string, string>("text", mensaje)
                ]);

                var response = await client.PostAsync(url, payload);
                statusCode = response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Telegram respondió {StatusCode}: {Body}", statusCode, body);

                try
                {
                    errorBody = JsonSerializer.Deserialize<TelegramErrorResponse>(body);
                }
                catch (JsonException)
                {
                    // Respuesta sin JSON valido (ej. error de un proxy intermedio); se sigue sin retry_after.
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar mensaje por Telegram.");
            }

            var esTransitorio = statusCode is null or HttpStatusCode.TooManyRequests || (int)statusCode.GetValueOrDefault() >= 500;
            if (intento == MaxIntentos || !esTransitorio)
            {
                return false;
            }

            var espera = statusCode == HttpStatusCode.TooManyRequests && errorBody?.Parameters?.RetryAfter is { } retryAfter
                ? TimeSpan.FromSeconds(Math.Min(retryAfter, TopeEsperaRateLimit.TotalSeconds))
                : EsperaEntreIntentos[Math.Min(intento - 1, EsperaEntreIntentos.Length - 1)];

            _logger.LogWarning("Reintentando envío a Telegram en {EsperaSegundos}s (intento {Intento}/{Max}).", espera.TotalSeconds, intento + 1, MaxIntentos);
            await Task.Delay(espera);
        }

        return false;
    }

    private sealed class TelegramErrorResponse
    {
        [JsonPropertyName("parameters")]
        public TelegramErrorParameters? Parameters { get; set; }
    }

    private sealed class TelegramErrorParameters
    {
        [JsonPropertyName("retry_after")]
        public int? RetryAfter { get; set; }
    }
}
