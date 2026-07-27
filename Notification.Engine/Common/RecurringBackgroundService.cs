namespace Notification.Engine.Common;

// Base para BackgroundService que corren en un intervalo fijo (PeriodicTimer)
public abstract class RecurringBackgroundService(TimeSpan interval, ILogger logger) : BackgroundService
{
    protected ILogger Logger => logger;

    protected abstract Task ProcessAsync(CancellationToken ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error ejecutando {Servicio}.", GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
