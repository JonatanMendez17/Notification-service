using Microsoft.Data.SqlClient;

namespace Notification.Engine.Data;

public interface ISqlDataAccess
{
    Task<List<T>> ConsultarAsync<T>(
        string sql,
        Func<SqlDataReader, T> map,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken ct = default);

    Task<int> EjecutarAsync(
        string sql,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken ct = default);

    Task<T?> ObtenerValorAsync<T>(
        string sql,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken ct = default);
}
