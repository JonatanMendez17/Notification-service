using Notification.Engine.Common;
using Notification.Engine.Data;
using Notification.Engine.Telegram;

namespace Notification.Engine.Jobs;

// Job 2 - Reprogramacion
// Logica de hitos reprogramados
public class ReprogramarJob(IServiceScopeFactory scopeFactory, ILogger<ReprogramarJob> logger) : MinuteBackgroundService(logger)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var hitosRepo = scope.ServiceProvider.GetRequiredService<IHitosRepository>();
        var telegram = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();

        var candidatos = await hitosRepo.ObtenerCandidatosReprogramarAsync(ct);
        if (candidatos.Count == 0)
        {
            Logger.LogInformation("ReprogramarJob: no es la hora de revisión, o no hay hitos.");
            return;
        }

        var hoy = DateTime.Now.Date;
        var manana = DateOnly.FromDateTime(hoy.AddDays(1));

        var vencidosSinRespuesta = candidatos
            .Where(h => h.Estado != "OK" && h.Reprogramar?.Date == hoy)
            .ToList();

        Logger.LogInformation("ReprogramarJob: {Total} hitos vencidos sin respuesta, reprogramando para mañana.", vencidosSinRespuesta.Count);

        foreach (var hito in vencidosSinRespuesta)
        {
            if (hito.MsgId is not null)
            {
                // Tolerante a fallos a propósito: si el mensaje ya no es editable, se sigue igual.
                await telegram.EditarMensajeAsync(hito.TggChatId, hito.MsgId, $"⏰ {hito.HitoTexto}", ct);
            }

            await hitosRepo.MarcarReprogramarAsync(hito.Id, manana, ct);
            Logger.LogInformation("ReprogramarJob: hito {HitoId} reprogramado para {Fecha}.", hito.Id, manana);
        }
    }
}
