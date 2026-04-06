using Microsoft.Extensions.Options;
using Notification.Api.Settings;

namespace Notification.Api.Providers;

public class TelegramProvider : INotificationProvider
{
    private const string ApiBase = "https://api.telegram.org";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramProvider> _logger;

    public string Canal => "Telegram";

    public TelegramProvider(IHttpClientFactory httpClientFactory, IOptions<TelegramSettings> settings, ILogger<TelegramProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<bool> EnviarAsync(string mensaje)
    {
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

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Telegram respondió {StatusCode}: {Body}", response.StatusCode, body);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar mensaje por Telegram.");
            return false;
        }
    }
}
