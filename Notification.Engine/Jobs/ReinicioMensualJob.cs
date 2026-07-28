using Notification.Engine.Common;
using Notification.Engine.Data;

namespace Notification.Engine.Jobs;

// Job 4 - Reinicio Mensual
// Reseteo mensual de hitos
public class ReinicioMensualJob(IServiceScopeFactory scopeFactory, ILogger<ReinicioMensualJob> logger) : HourlyBackgroundService(logger)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var hitosRepo = scope.ServiceProvider.GetRequiredService<IHitosRepository>();

        var candidatos = await hitosRepo.ObtenerCandidatosResetAsync(ct);
        if (candidatos.Count == 0)
        {
            Logger.LogInformation("ReinicioMensualJob: no es día/hora de reset, o no hay hitos en estado OK.");
            return;
        }

        // El SQL ya garantiza que hoy es día 1 o 15 (es la única forma de llegar acá).
        // Día 1: resetea todos los OK. Día 15: solo los OK con dia_mensual >= 15
        // (cubre un hito de la segunda quincena que quedó OK por respuesta tardía
        // de un ciclo anterior, posterior al reset del día 1).
        var esDiaUno = DateTime.Now.Day == 1;

        var idsAResetear = candidatos
            .Where(h => h.Estado == "OK" && (esDiaUno || h.DiaMensual >= 15))
            .Select(h => h.Id)
            .ToList();

        if (idsAResetear.Count > 0)
        {
            await hitosRepo.ResetearHitosAsync(idsAResetear, ct);
            Logger.LogInformation("ReinicioMensualJob: {Cantidad} hitos reseteados a Pendiente.", idsAResetear.Count);
        }
    }
}
