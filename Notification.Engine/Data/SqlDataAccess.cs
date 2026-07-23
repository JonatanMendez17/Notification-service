using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Notification.Engine.Settings;

namespace Notification.Engine.Data;

public class SqlDataAccess : ISqlDataAccess
{
    private readonly string _connectionString;

    public SqlDataAccess(IOptions<SqlSettings> settings)
    {
        _connectionString = settings.Value.ConnectionString;
    }

    public async Task<List<T>> QueryAsync<T>(
        string sql,
        Func<SqlDataReader, T> map,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        if (parameters is not null)
        {
            command.Parameters.AddRange(parameters.ToArray());
        }

        var resultados = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            resultados.Add(map(reader));
        }

        return resultados;
    }

    public async Task<int> ExecuteAsync(
        string sql,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        if (parameters is not null)
        {
            command.Parameters.AddRange(parameters.ToArray());
        }

        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<T?> ExecuteScalarAsync<T>(
        string sql,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken ct = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var command = new SqlCommand(sql, connection);
        if (parameters is not null)
        {
            command.Parameters.AddRange(parameters.ToArray());
        }

        var resultado = await command.ExecuteScalarAsync(ct);
        return resultado is null or DBNull ? default : (T)resultado;
    }
}
