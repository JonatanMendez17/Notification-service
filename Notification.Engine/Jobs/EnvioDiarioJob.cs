using Notification.Engine.Data;
using Notification.Engine.Services;
using Notification.Engine.Telegram;

namespace Notification.Engine.Jobs;

// Workflow 1 de N8N ("Envío diario"). Ver mapeo detallado en
// Recursos\plan-etapas-desarrollo.md. Trigger cada hora — la condición real de
// disparo por grupo vive en el propio SQL (HitosRepository), igual que en N8N.
public class EnvioDiarioJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EnvioDiarioJob> _logger;

    public EnvioDiarioJob(IServiceScopeFactory scopeFactory, ILogger<EnvioDiarioJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        do
        {
            try
            {
                await EjecutarAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ejecutando EnvioDiarioJob.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EjecutarAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var hitosRepo = scope.ServiceProvider.GetRequiredService<IHitosRepository>();
        var filtro = scope.ServiceProvider.GetRequiredService<IEnvioDiarioFilterService>();
        var telegram = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();

        var pendientes = await hitosRepo.GetPendientesEnvioDiarioAsync(ct);
        if (pendientes.Count == 0) return;

        var resultado = filtro.Filtrar(pendientes, DateTime.Now);

        foreach (var (hito, lunes) in resultado.MarcarLunes)
        {
            await hitosRepo.MarcarReprogramarAsync(hito.Id, lunes, ct);
        }

        foreach (var chatId in resultado.ChatsSinRecordatorios)
        {
            await telegram.SendMessageAsync(chatId, "✅ No hay recordatorios para hoy", ct: ct);
        }

        foreach (var (chatId, hitosDelChat) in resultado.HitosPorChat)
        {
            await telegram.SendMessageAsync(chatId, $"📅 *Recordatorio - {DateTime.Now:dd/MM/yyyy}*", ct: ct);

            foreach (var hito in hitosDelChat)
            {
                List<IReadOnlyList<InlineKeyboardButton>> teclado =
                [
                    [
                        new InlineKeyboardButton("✅OK", $"ok|{hito.Id}"),
                        new InlineKeyboardButton("⏰+1", $"posponer|{hito.Id}"),
                        new InlineKeyboardButton("⏰+2", $"posponer2|{hito.Id}"),
                        new InlineKeyboardButton("⏰+3", $"posponer3|{hito.Id}"),
                        new InlineKeyboardButton("⏰+4", $"posponer4|{hito.Id}")
                    ]
                ];

                var envio = await telegram.SendMessageAsync(chatId, $"- {hito.HitoTexto}", teclado, ct);

                if (envio is { Success: true, MessageId: { } messageId })
                {
                    await hitosRepo.GuardarEnvioAsync(hito.Id, messageId.ToString(), DateOnly.FromDateTime(DateTime.Now), ct);
                }
                else
                {
                    _logger.LogWarning("No se pudo enviar/guardar el hito {HitoId} al chat {ChatId}.", hito.Id, chatId);
                }
            }
        }
    }
}
