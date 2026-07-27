using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Notification.Engine.Settings;

namespace Notification.Engine.Telegram;

// Cliente directo de Telegram Bot API para soportar funciones específicas.
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
        var payload = new SendMessagePayload
        {
            ChatId = chatId,
            Text = text,
            ReplyMarkup = inlineKeyboard is { Count: > 0 }
                ? new ReplyMarkup { InlineKeyboard = inlineKeyboard }
                : null
        };

        var (exito, body) = await PostAsync("sendMessage", payload, $"chat {chatId}", ct);
        return new TelegramSendResult { Success = exito, MessageId = body?.Result?.MessageId };
    }

    // Actualiza mensajes enviados; si no pueden editarse, continúa sin interrumpir el proceso.
    public async Task<bool> EditMessageAsync(string chatId, string messageId, string text, CancellationToken ct = default)
    {
        var payload = new EditMessagePayload
        {
            ChatId = chatId,
            MessageId = messageId,
            Text = text,
            ReplyMarkup = new ReplyMarkup { InlineKeyboard = [] }
        };

        var (exito, _) = await PostAsync("editMessageText", payload, $"chat {chatId}, mensaje {messageId}", ct);
        return exito;
    }

    // Confirma visualmente la acción del botón; si falla, no interrumpe el procesamiento.
    public async Task<bool> AnswerCallbackQueryAsync(string callbackQueryId, CancellationToken ct = default)
    {
        var payload = new AnswerCallbackQueryPayload { CallbackQueryId = callbackQueryId };
        var (exito, _) = await PostAsync("answerCallbackQuery", payload, $"callback_query {callbackQueryId}", ct);
        return exito;
    }

    // Centraliza el POST a la Bot API: arma la URL, deserializa la respuesta y loguea warning/error de forma uniforme.
    private async Task<(bool Success, TelegramApiResponse? Body)> PostAsync<TPayload>(
        string metodo, TPayload payload, string contexto, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var url = $"{ApiBase}/bot{_settings.Token}/{metodo}";

            using var response = await client.PostAsJsonAsync(url, payload, ct);
            var body = await response.Content.ReadFromJsonAsync<TelegramApiResponse>(cancellationToken: ct);
            var exito = response.IsSuccessStatusCode && body is { Ok: true };

            if (!exito)
            {
                _logger.LogWarning("Telegram {Metodo} falló para {Contexto}: {StatusCode} {Descripcion}", metodo, contexto, response.StatusCode, body?.Description);
            }

            return (exito, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al llamar a Telegram {Metodo} para {Contexto}.", metodo, contexto);
            return (false, null);
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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ReplyMarkup? ReplyMarkup { get; set; }
    }

    private sealed class ReplyMarkup
    {
        [JsonPropertyName("inline_keyboard")]
        public IReadOnlyList<IReadOnlyList<InlineKeyboardButton>> InlineKeyboard { get; set; } = [];
    }
}
