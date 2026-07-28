using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Notification.Api.Settings;

namespace Notification.Api.Telegram;

// Fuente única del token del bot: se lee de dbo.Parametria (la misma fila que usa TGN Web
// en envio_manual.aspx y Notification.Engine), no de appsettings. Evita que cada proceso
// quede con su propia copia del token, potencialmente desincronizada — ver incidente
// 2026-07-28 (token revocado en Parametria mientras un appsettings tenía el vigente).
// Cacheado en memoria (Singleton): abre su propia conexión ADO.NET porque, a diferencia
// de Notification.Engine, esta Api no tiene una capa de datos genérica — agregarla solo
// para esta única consulta sería sobre-ingeniería.
public class TelegramTokenProvider(IOptions<SqlSettings> sqlSettings, ILogger<TelegramTokenProvider> logger)
{
    private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(1);
    private const string SqlToken = "SELECT par_valor FROM dbo.Parametria WHERE par_clave = 'telegram_bot_token' AND par_vigente = 1";

    private readonly SqlSettings _sqlSettings = sqlSettings.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTime _obtenidoEn = DateTime.MinValue;

    public async Task<string> ObtenerTokenAsync(CancellationToken ct = default)
    {
        if (_token is not null && DateTime.UtcNow - _obtenidoEn < DuracionCache)
        {
            return _token;
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTime.UtcNow - _obtenidoEn < DuracionCache)
            {
                return _token;
            }

            await using var connection = new SqlConnection(_sqlSettings.ConnectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand(SqlToken, connection);
            var resultado = await command.ExecuteScalarAsync(ct);
            var token = resultado as string;

            if (string.IsNullOrWhiteSpace(token))
            {
                logger.LogError("Token de Telegram no configurado en Parametria (par_clave = 'telegram_bot_token').");
                throw new InvalidOperationException("Token de Telegram no configurado.");
            }

            _token = token;
            _obtenidoEn = DateTime.UtcNow;
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }
}
