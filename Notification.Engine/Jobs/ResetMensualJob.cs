using Notification.Engine.Data;

namespace Notification.Engine.Jobs;

// Workflow 4 de N8N ("Reset mensual"). Ver mapeo detallado en
// Recursos\plan-etapas-desarrollo.md. Trigger cada hora — la condición de hora
// (Parametria.hora_reset) y de día (1 o 15) vive en el propio SQL.
public class ResetMensualJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResetMensualJob> _logger;

    public ResetMensualJob(IServiceScopeFactory scopeFactory, ILogger<ResetMensualJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        do
        {
            try
            {
                await EjecutarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando ResetMensualJob.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var hitosRepo = scope.ServiceProvider.GetRequiredService<IHitosRepository>();

        var candidatos = await hitosRepo.GetCandidatosResetAsync(ct);
        if (candidatos.Count == 0)
        {
            _logger.LogInformation("ResetMensualJob: no es día/hora de reset, o no hay hitos en estado OK.");
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
            _logger.LogInformation("ResetMensualJob: {Cantidad} hitos reseteados a Pendiente.", idsAResetear.Count);
        }
    }
}
