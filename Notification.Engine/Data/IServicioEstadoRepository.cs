namespace Notification.Engine.Data;

public interface IServicioEstadoRepository
{
    Task RegistrarHeartbeatAsync(string? version, CancellationToken ct = default);

    Task<DateTime?> ObtenerUltimoHeartbeatAsync(CancellationToken ct = default);
}
