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
            EnviaFinDeSemana: !reader.IsDBNull(ordEnviaFinSemana) && reader.GetInt32(ordEnviaFinSemana) == 1);
    }
}
