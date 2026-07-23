using Notification.Engine.Models;

namespace Notification.Engine.Data;

public interface IHitosRepository
{
    Task<List<Hito>> GetPendientesEnvioDiarioAsync(CancellationToken ct = default);

    Task MarcarReprogramarAsync(int hitoId, DateOnly fecha, CancellationToken ct = default);

    Task GuardarEnvioAsync(int hitoId, string messageId, DateOnly fecha, CancellationToken ct = default);
}
