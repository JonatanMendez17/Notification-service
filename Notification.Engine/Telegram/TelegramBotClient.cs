using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Notification.Engine.Settings;

namespace Notification.Engine.Telegram;

// Cliente propio a la Telegram Bot API. El Engine no usa Notification.Api para esto
// (spec sección 3): necesita botones inline y el message_id de vuelta, algo que
// el Api no ofrece ni tiene sentido que ofrezca para un gateway genérico.
public class TelegramBotClient
{
    private const string ApiBase = "https://api.telegram.org";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TelegramSettings _settings;
    private readonly ILogger<TelegramBotClient> _logger;

    public TelegramBotClient(IHttpClientFactory httpClientFactory, IOptions<TelegramSettings> settings, ILogger<TelegramBotClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<TelegramSendResult> SendMessageAsync(
        string chatId,
        string text,
        IReadOnlyList<IReadOnlyList<InlineKeyboardButton>>? inlineKeyboard = null,
        CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{ApiBase}/bot{_settings.Token}/sendMessage";

            var payload = new SendMessagePayload
            {
                ChatId = chatId,
                Text = text,
                ReplyMarkup = inlineKeyboard is { Count: > 0 }
                    ? new ReplyMarkup { InlineKeyboard = inlineKeyboard }
                    : null
            };

            using var response = await client.PostAsJsonAsync(url, payload, ct);
            var body = await response.Content.ReadFromJsonAsync<TelegramApiResponse>(cancellationToken: ct);

            if (!response.IsSuccessStatusCode || body is not { Ok: true })
            {
                _logger.LogWarning("Telegram sendMessage falló para chat {ChatId}: {StatusCode}", chatId, response.StatusCode);
                return new TelegramSendResult { Success = false };
            }

            return new TelegramSendResult { Success = true, MessageId = body.Result?.MessageId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar mensaje por Telegram al chat {ChatId}.", chatId);
            return new TelegramSendResult { Success = false };
        }
    }

    private sealed class SendMessagePayload
    {
        [JsonPropertyName("chat_id")]
        public string ChatId { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("reply_markup")]
        public ReplyMarkup? ReplyMarkup { get; set; }
    }

    private sealed class ReplyMarkup
    {
        [JsonPropertyName("inline_keyboard")]
        public IReadOnlyList<IReadOnlyList<InlineKeyboardButton>> InlineKeyboard { get; set; } = [];
    }
}
