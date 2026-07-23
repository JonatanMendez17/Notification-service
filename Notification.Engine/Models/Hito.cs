namespace Notification.Engine.Models;

public sealed record Hito(
    int Id,
    int DiaMensual,
    string HitoTexto,
    string Estado,
    DateTime? Reprogramar,
    string? MsgId,
    string TggChatId,
    bool EnviaFinDeSemana);
