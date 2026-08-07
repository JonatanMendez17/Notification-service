using System.Data;
using Microsoft.Data.SqlClient;

namespace Notification.Engine.Data;

// Guarda el heartbeat en dbo.Parametria (par_clave 'engine_heartbeat'/'engine_version') en vez
// de una tabla dedicada: es donde TGN Web ya expone config del sistema (conf_parametros.aspx),
// a costa de que un admin podría editar/borrar esas filas a mano desde ahí (decisión del usuario).
public class ServicioEstadoRepository(ISqlDataAccess db) : IServicioEstadoRepository
{
    private readonly ISqlDataAccess _db = db;

    public Task RegistrarHeartbeatAsync(string? version, CancellationToken ct = default) =>
        _db.EjecutarAsync(
            """
            UPDATE dbo.Parametria SET par_valor = CONVERT(VARCHAR(19), GETDATE(), 120) WHERE par_clave = 'engine_heartbeat'
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.Parametria (par_clave, par_valor, par_desc, par_vigente)
                VALUES ('engine_heartbeat', CONVERT(VARCHAR(19), GETDATE(), 120), 'Último aviso de vida de Notification.Engine (lo actualiza el propio servicio cada 30s, no editar a mano)', 1)

            UPDATE dbo.Parametria SET par_valor = @Version WHERE par_clave = 'engine_version'
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.Parametria (par_clave, par_valor, par_desc, par_vigente)
                VALUES ('engine_version', @Version, 'Versión de Notification.Engine actualmente en ejecución (la actualiza el propio servicio)', 1)
            """,
            [new SqlParameter("@Version", SqlDbType.VarChar, 20) { Value = version ?? string.Empty }],
            ct);

    public async Task<DateTime?> ObtenerUltimoHeartbeatAsync(CancellationToken ct = default)
    {
        var valor = await _db.ObtenerValorAsync<string>(
            "SELECT par_valor FROM dbo.Parametria WHERE par_clave = 'engine_heartbeat'",
            ct: ct);

        return DateTime.TryParse(valor, out var fecha) ? fecha : null;
    }
}
