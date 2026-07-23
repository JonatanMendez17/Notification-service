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

    // Usado por ReprogramarJob y ActualizacionesTiempoRealJob para actualizar un mensaje
    // ya enviado, siempre sin botones (reply_markup con inline_keyboard vacío los saca).
    // Tolerante a fallos a propósito (igual que "continueOnFail" en N8N): si el mensaje
    // ya no es editable (viejo, borrado), no tira excepción — el caller sigue con el resto.
    public async Task<bool> EditMessageAsync(string chatId, string messageId, string text, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{ApiBase}/bot{_settings.Token}/editMessageText";

            var payload = new EditMessagePayload
            {
                ChatId = chatId,
                MessageId = messageId,
                Text = text,
                ReplyMarkup = new ReplyMarkup { InlineKeyboard = [] }
            };

            using var response = await client.PostAsJsonAsync(url, payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram editMessageText falló para chat {ChatId}, mensaje {MessageId}: {StatusCode}", chatId, messageId, response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al editar mensaje de Telegram. Chat {ChatId}, mensaje {MessageId}.", chatId, messageId);
            return false;
        }
    }

    // Ack visual del botón tocado (el "loading" del cliente de Telegram). Tolerante a
    // fallos: si falla, no bloquea el resto del procesamiento del callback.
    public async Task<bool> AnswerCallbackQueryAsync(string callbackQueryId, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{ApiBase}/bot{_settings.Token}/answerCallbackQuery";

            var payload = new AnswerCallbackQueryPayload { CallbackQueryId = callbackQueryId };

            using var response = await client.PostAsJsonAsync(url, payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram answerCallbackQuery falló para {CallbackQueryId}: {StatusCode}", callbackQueryId, response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al responder callback_query {CallbackQueryId}.", callbackQueryId);
            return false;
        }
    }

    private sealed class AnswerCallbackQueryPayload
    {
        [JsonPropertyName("callback_query_id")]
        public string CallbackQueryId { get; set; } = string.Empty;
    }

    private sealed class EditMessagePayload
    {
        [JsonPropertyName("chat_id")]
        public string ChatId { get; set; } = string.Empty;

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("reply_markup")]
        public ReplyMarkup? ReplyMarkup { get; set; }
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
