using Notification.Engine.Data;
using Notification.Engine.Telegram;

namespace Notification.Engine.Jobs;

// Workflow 3 de N8N ("Reprogramar"). Ver mapeo detallado en
// Recursos\plan-etapas-desarrollo.md. Trigger cada hora — la condición de hora
// (Parametria.hora_revision) vive en el propio SQL, igual que EnvioDiarioJob.
public class ReprogramarJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReprogramarJob> _logger;

    public ReprogramarJob(IServiceScopeFactory scopeFactory, ILogger<ReprogramarJob> logger)
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
                _logger.LogError(ex, "Error ejecutando ReprogramarJob.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var hitosRepo = scope.ServiceProvider.GetRequiredService<IHitosRepository>();
        var telegram = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();

        var candidatos = await hitosRepo.GetCandidatosReprogramarAsync(ct);
        if (candidatos.Count == 0)
        {
            _logger.LogInformation("ReprogramarJob: no es la hora de revisión, o no hay hitos.");
            return;
        }

        var hoy = DateTime.Now.Date;
        var manana = DateOnly.FromDateTime(hoy.AddDays(1));

        var vencidosSinRespuesta = candidatos
            .Where(h => h.Estado != "OK" && h.Reprogramar?.Date == hoy)
            .ToList();

        _logger.LogInformation("ReprogramarJob: {Total} hitos vencidos sin respuesta, reprogramando para mañana.", vencidosSinRespuesta.Count);

        foreach (var hito in vencidosSinRespuesta)
        {
            if (hito.MsgId is not null)
            {
                // Tolerante a fallos a propósito: si el mensaje ya no es editable, se sigue igual.
                await telegram.EditMessageAsync(hito.TggChatId, hito.MsgId, $"⏰ {hito.HitoTexto}", ct);
            }

            await hitosRepo.MarcarReprogramarAsync(hito.Id, manana, ct);
            _logger.LogInformation("ReprogramarJob: hito {HitoId} reprogramado para {Fecha}.", hito.Id, manana);
        }
    }
}
