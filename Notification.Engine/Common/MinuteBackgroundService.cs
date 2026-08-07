namespace Notification.Engine.Common;

// Base para jobs que deben correr una vez por minuto, alineados al reloj (HH:mm:00) — mismo
// criterio que HourlyBackgroundService pero a nivel minuto. La necesitan los jobs cuya condición
// de disparo en SQL ahora compara HH:mm completo (hora de envío/revisión/reset con minuto libre,
// no solo en punto).
public abstract class MinuteBackgroundService(ILogger logger) : BackgroundService
{
    protected ILogger Logger => logger;

    protected abstract Task ProcessAsync(CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error ejecutando {Servicio}.", GetType().Name);
            }

            var ahora = DateTime.Now;
            var inicioMinutoActual = new DateTime(ahora.Year, ahora.Month, ahora.Day, ahora.Hour, ahora.Minute, 0);
            var proximoMinutoEnPunto = inicioMinutoActual.AddMinutes(1);
            var espera = proximoMinutoEnPunto - ahora;

            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
