namespace Notification.Engine.Common;

// Base para BackgroundService que corren en un intervalo fijo (PeriodicTimer)
// y no deben morir si una iteración tira excepción.
public abstract class PeriodicBackgroundService(TimeSpan interval, ILogger logger) : BackgroundService
{
    protected ILogger Logger => logger;

    protected abstract Task EjecutarAsync(CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await EjecutarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error ejecutando {Servicio}.", GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
