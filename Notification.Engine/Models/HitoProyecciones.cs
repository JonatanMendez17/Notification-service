namespace Notification.Engine.Models;

// Proyecciones por job: cada workflow usa solo las columnas que necesita.

public sealed record HitoParaReprogramar(
    int Id,
    string HitoTexto,
    string Estado,
    DateTime? Reprogramar,
    string? MsgId,
    string TggChatId);

public sealed record HitoParaReset(
    int Id,
    int DiaMensual,
    string Estado);

public sealed record HitoParaActualizar(
    int Id,
    string HitoTexto,
    string Estado,
    string MsgId,
    string TggChatId);
