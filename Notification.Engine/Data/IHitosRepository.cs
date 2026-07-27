using Notification.Engine.Models;

namespace Notification.Engine.Data;

public interface IHitosRepository
{
    // Envío diario
    Task<List<Hito>> GetPendientesEnvioDiarioAsync(CancellationToken ct = default);

    Task MarcarReprogramarAsync(int hitoId, DateOnly fecha, CancellationToken ct = default);

    Task GuardarEnvioAsync(int hitoId, string messageId, DateOnly fecha, CancellationToken ct = default);

    // Reprogramar
    Task<List<HitoParaReprogramar>> GetCandidatosReprogramarAsync(CancellationToken ct = default);

    // Reset mensual
    Task<List<HitoParaReset>> GetCandidatosResetAsync(CancellationToken ct = default);

    Task ResetearHitosAsync(IReadOnlyList<int> hitoIds, CancellationToken ct = default);

    // Actualizaciones en tiempo real
    Task<List<HitoParaActualizar>> GetPendientesActualizarAsync(CancellationToken ct = default);

    Task LimpiarFlagActualizarAsync(int hitoId, CancellationToken ct = default);

    // Respuesta y registro. 0 filas afectadas indica un callback duplicado.
    Task<int> RegistrarHitoOkAsync(int hitoId, long tgUserId, string nombreCompleto, CancellationToken ct = default);

    Task<int> RegistrarHitoPospuestoAsync(int hitoId, DateOnly nuevaFecha, string accionTexto, long tgUserId, string nombreCompleto, CancellationToken ct = default);
}
