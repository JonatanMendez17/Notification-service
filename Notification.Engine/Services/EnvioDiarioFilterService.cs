using Notification.Engine.Models;

namespace Notification.Engine.Services;

// Puerto 1:1 del nodo Code "Filtrar" del workflow "1. Envío diario" de N8N.
// Ver mapeo detallado en Recursos\plan-etapas-desarrollo.md.
public class EnvioDiarioFilterService : IEnvioDiarioFilterService
{
    public FiltroEnvioDiarioResultado Filtrar(IReadOnlyList<Hito> hitos, DateTime ahora)
    {
        var hoy = ahora.Date;
        var hoyDia = ahora.Day;
        var ultimoDiaMes = DateTime.DaysInMonth(ahora.Year, ahora.Month);
        var esFinDeSemanaHoy = ahora.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var esViernes = ahora.DayOfWeek == DayOfWeek.Friday;

        var sabado = hoy.AddDays(1);
        var domingo = hoy.AddDays(2);

        var matching = new List<Hito>();
        var marcarLunes = new List<Hito>();
        var idsIncluidos = new HashSet<int>();

        bool EstaVencidoHoy(Hito h)
        {
            if (h.DiaMensual == hoyDia) return true;
            var fecha = h.Reprogramar?.Date;
            return fecha is not null && fecha <= hoy;
        }

        foreach (var h in hitos)
        {
            if (h.Estado == "OK") continue;
            var habilesSolo = !h.EnviaFinDeSemana;

            if (EstaVencidoHoy(h))
            {
                if (habilesSolo && esFinDeSemanaHoy) marcarLunes.Add(h);
                else matching.Add(h);
                idsIncluidos.Add(h.Id);
                continue;
            }

            // Adelanto de viernes: solo para grupos "hábiles" (no reciben sáb/dom, se les manda antes)
            if (habilesSolo && esViernes)
            {
                var fecha = h.Reprogramar?.Date;
                var esSabado = h.DiaMensual == sabado.Day || fecha == sabado;
                var esDomingo = h.DiaMensual == domingo.Day || fecha == domingo;
                if (esSabado || esDomingo)
                {
                    matching.Add(h);
                    idsIncluidos.Add(h.Id);
                }
            }
        }

        // Último día del mes: incluir hitos de días inexistentes en este mes (ej: 29,30,31 en febrero)
        if (hoyDia == ultimoDiaMes)
        {
            foreach (var h in hitos)
            {
                if (h.Estado == "OK" || idsIncluidos.Contains(h.Id)) continue;
                if (h.DiaMensual <= ultimoDiaMes) continue;

                var habilesSolo = !h.EnviaFinDeSemana;
                if (habilesSolo && esFinDeSemanaHoy) marcarLunes.Add(h);
                else matching.Add(h);
            }
        }

        var marcarLunesConFecha = new List<(Hito, DateOnly)>();
        if (marcarLunes.Count > 0)
        {
            var diasHastaLunes = ahora.DayOfWeek == DayOfWeek.Saturday ? 2 : 1;
            var lunes = DateOnly.FromDateTime(hoy.AddDays(diasHastaLunes));
            marcarLunesConFecha.AddRange(marcarLunes.Select(h => (h, lunes)));
        }

        var agrupados = matching
            .GroupBy(h => h.TggChatId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Hito>)g.ToList());

        // Por chat: si un grupo activo hoy no tiene ningún hito en "matching",
        // recibe "sin recordatorios" — independiente de lo que pase en otros grupos.
        var chatsSinRecordatorios = ObtenerChatsActivosHoy(hitos, esFinDeSemanaHoy)
            .Where(chatId => !agrupados.ContainsKey(chatId))
            .ToList();

        return new FiltroEnvioDiarioResultado(marcarLunesConFecha, chatsSinRecordatorios, agrupados);
    }

    private static IReadOnlyList<string> ObtenerChatsActivosHoy(IReadOnlyList<Hito> hitos, bool esFinDeSemanaHoy)
    {
        var chatFlags = hitos
            .GroupBy(h => h.TggChatId)
            .ToDictionary(g => g.Key, g => g.Last().EnviaFinDeSemana);

        var todosLosChats = hitos
            .Select(h => h.TggChatId)
            .Where(id => !string.IsNullOrWhiteSpace(id) && int.TryParse(id, out var n) && n != 0)
            .Distinct()
            .ToList();

        return esFinDeSemanaHoy
            ? todosLosChats.Where(id => chatFlags.TryGetValue(id, out var v) && v).ToList()
            : todosLosChats;
    }
}
