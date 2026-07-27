using Notification.Engine.Common;
using Notification.Engine.Data;
using Notification.Engine.Telegram;

namespace Notification.Engine.Jobs;

// Job 3 - ActualizacionEnTiempoReal
// Sincronizacion en Telegram de cambios realizados desde la app web.
public class ActualizacionesTiempoRealJob : PeriodicBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ActualizacionesTiempoRealJob(IServiceScopeFactory scopeFactory, ILogger<ActualizacionesTiempoRealJob> logger)
        : base(TimeSpan.FromSeconds(5), logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task EjecutarAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var hitosRepo = scope.ServiceProvider.GetRequiredService<IHitosRepository>();
        var telegram = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();

        // Corre cada 5s — no se loguea el caso "sin novedades" para no inundar la consola.
        var pendientes = await hitosRepo.GetPendientesActualizarAsync(ct);
        if (pendientes.Count == 0) return;

        Logger.LogInformation("ActualizacionesTiempoRealJob: {Cantidad} correcciones de la app para sincronizar a Telegram.", pendientes.Count);

        foreach (var hito in pendientes)
        {
            var texto = hito.Estado == "OK" ? $"✅OK-{hito.HitoTexto}" : $"⏰Pospuesto-{hito.HitoTexto}";

            // Tolerante a fallos a propósito: si el mensaje ya no existe/es viejo, el flag se limpia igual.
            await telegram.EditMessageAsync(hito.TggChatId, hito.MsgId, texto, ct);
            await hitosRepo.LimpiarFlagActualizarAsync(hito.Id, ct);
            Logger.LogInformation("ActualizacionesTiempoRealJob: hito {HitoId} sincronizado.", hito.Id);
        }
    }
}
