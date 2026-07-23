using Notification.Engine.Models;

namespace Notification.Engine.Services;

public sealed record FiltroEnvioDiarioResultado(
    IReadOnlyList<(Hito Hito, DateOnly Lunes)> MarcarLunes,
    IReadOnlyList<string> ChatsSinRecordatorios,
    IReadOnlyDictionary<string, IReadOnlyList<Hito>> HitosPorChat);

public interface IEnvioDiarioFilterService
{
    FiltroEnvioDiarioResultado Filtrar(IReadOnlyList<Hito> hitos, DateTime ahora);
}
