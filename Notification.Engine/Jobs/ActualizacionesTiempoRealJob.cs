using Notification.Engine.Data;
using Notification.Engine.Telegram;

namespace Notification.Engine.Jobs;

// Workflow 5 de N8N ("Actualizaciones en tiempo real"). Ver mapeo detallado en
// Recursos\plan-etapas-desarrollo.md. Trigger cada 5s — sincroniza hacia Telegram
// correcciones hechas desde la app web (tg_actualizar = 1).
public class ActualizacionesTiempoRealJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ActualizacionesTiempoRealJob> _logger;

    public ActualizacionesTiempoRealJob(IServiceScopeFactory scopeFactory, ILogger<ActualizacionesTiempoRealJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        do
        {
            try
            {
                await EjecutarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando ActualizacionesTiempoRealJob.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var hitosRepo = scope.ServiceProvider.GetRequiredService<IHitosRepository>();
        var telegram = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();

        // Corre cada 5s — no se loguea el caso "sin novedades" para no inundar la consola.
        var pendientes = await hitosRepo.GetPendientesActualizarAsync(ct);
        if (pendientes.Count == 0) return;

        _logger.LogInformation("ActualizacionesTiempoRealJob: {Cantidad} correcciones de la app para sincronizar a Telegram.", pendientes.Count);

        foreach (var hito in pendientes)
        {
            var texto = hito.Estado == "OK" ? $"✅OK-{hito.HitoTexto}" : $"⏰Pospuesto-{hito.HitoTexto}";

            // Tolerante a fallos a propósito: si el mensaje ya no existe/es viejo, el flag se limpia igual.
            await telegram.EditMessageAsync(hito.TggChatId, hito.MsgId, texto, ct);
            await hitosRepo.LimpiarFlagActualizarAsync(hito.Id, ct);
            _logger.LogInformation("ActualizacionesTiempoRealJob: hito {HitoId} sincronizado.", hito.Id);
        }
    }
}
