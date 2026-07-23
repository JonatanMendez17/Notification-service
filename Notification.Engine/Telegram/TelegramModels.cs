using System.Text.Json.Serialization;

namespace Notification.Engine.Telegram;

public sealed record InlineKeyboardButton(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("callback_data")] string CallbackData);

public sealed class TelegramSendResult
{
    public bool Success { get; init; }
    public long? MessageId { get; init; }
}

internal sealed class TelegramApiResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("result")]
    public TelegramApiMessage? Result { get; set; }
}

internal sealed class TelegramApiMessage
{
    [JsonPropertyName("message_id")]
    public long MessageId { get; set; }
}
