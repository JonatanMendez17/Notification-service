namespace Notification.Engine.Models;

// Proyecciones de lectura específicas por job — cada workflow original de N8N
// selecciona columnas distintas, se respeta esa diferencia acá en vez de forzar
// un único modelo "Hito" con campos que algunos jobs no usan.

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
