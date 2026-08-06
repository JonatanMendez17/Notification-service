using System.Reflection;
using Notification.Engine.Common;
using Notification.Engine.Data;

namespace Notification.Engine.Jobs;

// Job 6 - Heartbeat
// Reporta que el proceso sigue vivo (dbo.Parametria: 'engine_heartbeat'/'engine_version');
// TGN Web lo lee en el Dashboard (inicio.aspx) para mostrar si el Engine está activo o caído.
public class HeartbeatJob(IServiceScopeFactory scopeFactory, ILogger<HeartbeatJob> logger) : RecurringBackgroundService(TimeSpan.FromSeconds(30), logger)
{
    private static readonly string? Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IServicioEstadoRepository>();

        await repo.RegistrarHeartbeatAsync(Version, ct);
    }
}
