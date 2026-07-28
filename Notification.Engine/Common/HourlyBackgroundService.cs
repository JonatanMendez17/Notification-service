namespace Notification.Engine.Common;

// Base para jobs que deben correr una vez por hora, alineados al reloj (HH:00:00) —
// no a un intervalo fijo desde que arrancó el proceso (eso es lo que hacía correr,
// por ejemplo, un job "de las 12:00" recién a las 12:46 si el servicio había arrancado
// a las 11:46). La ventana de negocio sigue siendo "hora actual == hora configurada"
// (DATEPART(hour, GETDATE()) en el SQL, válida de HH:00:00 a HH:59:59) — lo único que
// cambia es CUÁNDO se hace el chequeo dentro de esa ventana: ahora siempre al toque de
// que empieza la hora, no en un punto arbitrario.
// Corre una vez de entrada al arrancar (por si algo ya está vencido dentro de la hora
// actual justo cuando el proceso se levanta) y después, siempre, en cada hora en punto.
public abstract class HourlyBackgroundService(ILogger logger) : BackgroundService
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
            var proximaHoraEnPunto = ahora.Date.AddHours(ahora.Hour + 1);
            var espera = proximaHoraEnPunto - ahora;

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
