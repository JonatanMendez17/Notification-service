using Notification.Engine.Data;

namespace Notification.Engine.Telegram;

// Fuente única del token del bot: se lee de dbo.Parametria (la misma fila que usa TGN Web
// en envio_manual.aspx), no de appsettings. Evita que Engine/Api y TGN queden con copias
// del token desincronizadas — ver incidente 2026-07-28 (token revocado en Parametria
// mientras el appsettings del Engine tenía el vigente).
// Cacheado en memoria: registrado como Singleton (TelegramBotClient y PollingReceiver
// también lo son) pero ISqlDataAccess es Scoped, por eso resuelve vía IServiceScopeFactory.
public class TelegramTokenProvider(IServiceScopeFactory scopeFactory, ILogger<TelegramTokenProvider> logger)
{
    private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(1);
    private const string SqlToken = "SELECT par_valor FROM dbo.Parametria WHERE par_clave = 'telegram_bot_token' AND par_vigente = 1";

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

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ISqlDataAccess>();
            var token = await db.ObtenerValorAsync<string>(SqlToken, ct: ct);

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
