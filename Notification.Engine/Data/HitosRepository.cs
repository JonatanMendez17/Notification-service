using System.Data;
using Microsoft.Data.SqlClient;
using Notification.Engine.Models;

namespace Notification.Engine.Data;

public class HitosRepository : IHitosRepository
{
    // Misma condición que el workflow "1. Envío diario" de N8N: hora del grupo (Tgg_Hora_Envio)
    // o, si no está seteada, la hora global de Parametria ('hora_envio_diario').
    private const string SqlPendientesEnvioDiario = """
        SELECT h.id, h.dia_mensual, h.hito, h.estado, h.reprogramar, h.msg_id,
               t.Tgg_Chat_Id, t.Tgg_Envia_Fin_Semana
        FROM dbo.Hitos_Mensuales h
        JOIN dbo.Tg_Grupo t ON t.Tgg_Id = h.Tgg_id
        WHERE t.Tgg_Estado = 1
          AND t.Tgg_Chat_Id IS NOT NULL
          AND LTRIM(RTRIM(CAST(t.Tgg_Chat_Id AS varchar))) != ''
          AND DATEPART(hour, GETDATE()) = ISNULL(
                CAST(LEFT(t.Tgg_Hora_Envio, 2) AS INT),
                (SELECT CAST(LEFT(par_valor, 2) AS TINYINT) FROM dbo.Parametria WHERE par_clave = 'hora_envio_diario' AND par_vigente = 1))
        """;

    // Workflow "3. Reprogramar": trae todos los hitos condicionados a la hora de revisión
    // (Parametria.hora_revision); el filtro de "reprogramar = hoy y estado != OK" se hace
    // en C# (EnvioDiarioFilterService no aplica acá, es lógica simple, va directo en el Job).
    private const string SqlCandidatosReprogramar = """
        SELECT h.id, h.hito, h.estado, h.reprogramar, h.msg_id, t.Tgg_Chat_Id
        FROM dbo.Hitos_Mensuales h
        JOIN dbo.Tg_Grupo t ON t.Tgg_Id = h.Tgg_Id
        WHERE DATEPART(hour, GETDATE()) = (
            SELECT CAST(LEFT(par_valor, 2) AS TINYINT) FROM dbo.Parametria WHERE par_clave = 'hora_revision' AND par_vigente = 1)
        """;

    // Workflow "4. Reset mensual": condicionado a hora_reset y a que hoy sea día 1 o 15.
    private const string SqlCandidatosReset = """
        SELECT id, dia_mensual, estado
        FROM dbo.Hitos_Mensuales
        WHERE DATEPART(hour, GETDATE()) = (
            SELECT CAST(LEFT(par_valor, 2) AS TINYINT) FROM dbo.Parametria WHERE par_clave = 'hora_reset' AND par_vigente = 1)
          AND (DAY(GETDATE()) = 1 OR DAY(GETDATE()) = 15)
        """;

    // Workflow "5. Actualizaciones en tiempo real": correcciones hechas desde la app web
    // que todavía no se reflejaron en Telegram.
    private const string SqlPendientesActualizar = """
        SELECT h.id, h.hito, h.estado, h.msg_id, t.Tgg_Chat_Id
        FROM dbo.Hitos_Mensuales h
        JOIN dbo.Tg_Grupo t ON t.Tgg_Id = h.Tgg_Id
        WHERE h.tg_actualizar = 1
          AND h.msg_id IS NOT NULL
          AND h.msg_id != ''
        """;

    private readonly ISqlDataAccess _db;

    public HitosRepository(ISqlDataAccess db)
    {
        _db = db;
    }

    public Task<List<Hito>> GetPendientesEnvioDiarioAsync(CancellationToken ct = default) =>
        _db.QueryAsync(SqlPendientesEnvioDiario, Map, ct: ct);

    public Task MarcarReprogramarAsync(int hitoId, DateOnly fecha, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            "UPDATE dbo.Hitos_Mensuales SET reprogramar = @Fecha WHERE id = @Id",
            [
                new SqlParameter("@Fecha", SqlDbType.Date) { Value = fecha.ToDateTime(TimeOnly.MinValue) },
                new SqlParameter("@Id", SqlDbType.Int) { Value = hitoId }
            ],
            ct);

    public Task GuardarEnvioAsync(int hitoId, string messageId, DateOnly fecha, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            "UPDATE dbo.Hitos_Mensuales SET msg_id = @MsgId, reprogramar = @Fecha WHERE id = @Id",
            [
                new SqlParameter("@MsgId", SqlDbType.NVarChar, 50) { Value = messageId },
                new SqlParameter("@Fecha", SqlDbType.Date) { Value = fecha.ToDateTime(TimeOnly.MinValue) },
                new SqlParameter("@Id", SqlDbType.Int) { Value = hitoId }
            ],
            ct);

    public Task<List<HitoParaReprogramar>> GetCandidatosReprogramarAsync(CancellationToken ct = default) =>
        _db.QueryAsync(SqlCandidatosReprogramar, MapReprogramar, ct: ct);

    public Task<List<HitoParaReset>> GetCandidatosResetAsync(CancellationToken ct = default) =>
        _db.QueryAsync(SqlCandidatosReset, MapReset, ct: ct);

    public Task ResetearHitosAsync(IReadOnlyList<int> hitoIds, CancellationToken ct = default)
    {
        if (hitoIds.Count == 0) return Task.CompletedTask;

        var nombresParametros = hitoIds.Select((_, i) => $"@Id{i}").ToArray();
        var parametros = hitoIds.Select((id, i) => new SqlParameter($"@Id{i}", SqlDbType.Int) { Value = id });

        var sql = $"UPDATE dbo.Hitos_Mensuales SET estado = 'Pendiente', reprogramar = NULL WHERE id IN ({string.Join(",", nombresParametros)})";

        return _db.ExecuteAsync(sql, parametros, ct);
    }

    public Task<List<HitoParaActualizar>> GetPendientesActualizarAsync(CancellationToken ct = default) =>
        _db.QueryAsync(SqlPendientesActualizar, MapActualizar, ct: ct);

    public Task LimpiarFlagActualizarAsync(int hitoId, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            "UPDATE dbo.Hitos_Mensuales SET tg_actualizar = 0 WHERE id = @Id",
            [new SqlParameter("@Id", SqlDbType.Int) { Value = hitoId }],
            ct);

    public Task RegistrarHitoOkAsync(int hitoId, long tgUserId, string nombreCompleto, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.Hitos_Mensuales
            SET estado = 'OK', Ultima_Respuesta_Tg_Id = @TgId, Ultima_Respuesta_Nombre = @Nombre,
                Ultima_Respuesta_Accion = 'OK', Ultima_Respuesta_Fecha = GETDATE()
            WHERE id = @Id
            """,
            [
                new SqlParameter("@TgId", SqlDbType.BigInt) { Value = tgUserId },
                new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = nombreCompleto },
                new SqlParameter("@Id", SqlDbType.Int) { Value = hitoId }
            ],
            ct);

    public Task RegistrarHitoPospuestoAsync(int hitoId, DateOnly nuevaFecha, string accionTexto, long tgUserId, string nombreCompleto, CancellationToken ct = default) =>
        _db.ExecuteAsync(
            """
            UPDATE dbo.Hitos_Mensuales
            SET reprogramar = @Fecha, estado = 'Pendiente', Ultima_Respuesta_Tg_Id = @TgId,
                Ultima_Respuesta_Nombre = @Nombre, Ultima_Respuesta_Accion = @Accion, Ultima_Respuesta_Fecha = GETDATE()
            WHERE id = @Id
            """,
            [
                new SqlParameter("@Fecha", SqlDbType.Date) { Value = nuevaFecha.ToDateTime(TimeOnly.MinValue) },
                new SqlParameter("@TgId", SqlDbType.BigInt) { Value = tgUserId },
                new SqlParameter("@Nombre", SqlDbType.NVarChar, 200) { Value = nombreCompleto },
                new SqlParameter("@Accion", SqlDbType.VarChar, 20) { Value = accionTexto },
                new SqlParameter("@Id", SqlDbType.Int) { Value = hitoId }
            ],
            ct);

    private static HitoParaReprogramar MapReprogramar(SqlDataReader reader)
    {
        var ordReprogramar = reader.GetOrdinal("reprogramar");
        var ordMsgId = reader.GetOrdinal("msg_id");

        return new HitoParaReprogramar(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            HitoTexto: reader.GetString(reader.GetOrdinal("hito")),
            Estado: reader.GetString(reader.GetOrdinal("estado")),
            Reprogramar: reader.IsDBNull(ordReprogramar) ? null : reader.GetDateTime(ordReprogramar),
            MsgId: reader.IsDBNull(ordMsgId) ? null : reader.GetString(ordMsgId),
            TggChatId: reader.GetString(reader.GetOrdinal("Tgg_Chat_Id")));
    }

    private static HitoParaReset MapReset(SqlDataReader reader) => new(
        Id: reader.GetInt32(reader.GetOrdinal("id")),
        DiaMensual: reader.GetInt32(reader.GetOrdinal("dia_mensual")),
        Estado: reader.GetString(reader.GetOrdinal("estado")));

    private static HitoParaActualizar MapActualizar(SqlDataReader reader) => new(
        Id: reader.GetInt32(reader.GetOrdinal("id")),
        HitoTexto: reader.GetString(reader.GetOrdinal("hito")),
        Estado: reader.GetString(reader.GetOrdinal("estado")),
        MsgId: reader.GetString(reader.GetOrdinal("msg_id")),
        TggChatId: reader.GetString(reader.GetOrdinal("Tgg_Chat_Id")));

    private static Hito Map(SqlDataReader reader)
    {
        var ordEstado = reader.GetOrdinal("estado");
        var ordReprogramar = reader.GetOrdinal("reprogramar");
        var ordMsgId = reader.GetOrdinal("msg_id");
        var ordEnviaFinSemana = reader.GetOrdinal("Tgg_Envia_Fin_Semana");

        return new Hito(
            Id: reader.GetInt32(reader.GetOrdinal("id")),
            DiaMensual: reader.GetInt32(reader.GetOrdinal("dia_mensual")),
            HitoTexto: reader.GetString(reader.GetOrdinal("hito")),
            Estado: reader.IsDBNull(ordEstado) ? string.Empty : reader.GetString(ordEstado),
            Reprogramar: reader.IsDBNull(ordReprogramar) ? null : reader.GetDateTime(ordReprogramar),
            MsgId: reader.IsDBNull(ordMsgId) ? null : reader.GetString(ordMsgId),
            TggChatId: reader.GetString(reader.GetOrdinal("Tgg_Chat_Id")),
            EnviaFinDeSemana: !reader.IsDBNull(ordEnviaFinSemana) && reader.GetBoolean(ordEnviaFinSemana));
    }
}
