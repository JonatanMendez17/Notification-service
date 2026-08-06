namespace Notification.Engine.Data;

public interface IServicioEstadoRepository
{
    Task RegistrarHeartbeatAsync(string? version, CancellationToken ct = default);
}
