using System.Reflection;
using Notification.Engine.Common;
using Notification.Engine.Data;

namespace Notification.Engine.Jobs;

// Job 6 - Heartbeat
// Reporta que el proceso sigue vivo (dbo.Parametria: 'engine_heartbeat'/'engine_version');
// TGN Web lo lee en el Dashboard (inicio.aspx) para mostrar si el Engine está activo o caído.
public class HeartbeatJob(IServiceScopeFactory scopeFactory, ILogger<HeartbeatJob> logger) : RecurringBackgroundService(TimeSpan.FromSeconds(30), logger)
{
    // Por debajo de esto, el hueco entre heartbeats es el de un restart normal (deploy, etc.), no una caída real.
    private static readonly TimeSpan UmbralCaida = TimeSpan.FromMinutes(2);

    private static readonly string? Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString();

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private bool _primeraEjecucion = true;

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IServicioEstadoRepository>();

        if (_primeraEjecucion)
        {
            _primeraEjecucion = false;
            await AvisarSiHuboCaidaAsync(repo, ct);
        }

        await repo.RegistrarHeartbeatAsync(Version, ct);
    }

    private async Task AvisarSiHuboCaidaAsync(IServicioEstadoRepository repo, CancellationToken ct)
    {
        var ultimoHeartbeat = await repo.ObtenerUltimoHeartbeatAsync(ct);
        if (ultimoHeartbeat is null)
        {
            return;
        }

        var caidoDesde = DateTime.Now - ultimoHeartbeat.Value;
        if (caidoDesde > UmbralCaida)
        {
            Logger.LogWarning(
                "Notification.Engine CAÍDO detectado al arrancar: sin heartbeat desde {UltimoHeartbeat:yyyy-MM-dd HH:mm:ss} ({Minutos} min).",
                ultimoHeartbeat.Value,
                (int)caidoDesde.TotalMinutes);
        }
    }
}
