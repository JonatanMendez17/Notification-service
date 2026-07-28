using Notification.Engine.Common;
using Notification.Engine.Data;
using Notification.Engine.Services;
using Notification.Engine.Telegram;

namespace Notification.Engine.Jobs;

// Job 1 - Envio Diario
// Lógica de ejecución de recodatorios diarios
public class EnvioDiarioJob(IServiceScopeFactory scopeFactory, ILogger<EnvioDiarioJob> logger) : HourlyBackgroundService(logger)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;

    protected override async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var hitosRepo = scope.ServiceProvider.GetRequiredService<IHitosRepository>();
        var filtro = scope.ServiceProvider.GetRequiredService<IEnvioDiarioFilterService>();
        var telegram = scope.ServiceProvider.GetRequiredService<TelegramBotClient>();

        var pendientes = await hitosRepo.ObtenerPendientesEnvioDiarioAsync(ct);
        if (pendientes.Count == 0)
        {
            Logger.LogInformation("EnvioDiarioJob: sin grupos/hitos para esta hora.");
            return;
        }

        var resultado = filtro.Filtrar(pendientes, DateTime.Now);
        Logger.LogInformation(
            "EnvioDiarioJob: {Total} hitos leídos — {Lunes} a reprogramar al lunes, {SinRecordatorio} chats sin recordatorios, {ConHitos} chats con hitos a enviar.",
            pendientes.Count, resultado.MarcarLunes.Count, resultado.ChatsSinRecordatorios.Count, resultado.HitosPorChat.Count);

        foreach (var (hito, lunes) in resultado.MarcarLunes)
        {
            await hitosRepo.MarcarReprogramarAsync(hito.Id, lunes, ct);
        }

        foreach (var chatId in resultado.ChatsSinRecordatorios)
        {
            await telegram.EnviarMensajeAsync(chatId, "✅ No hay recordatorios para hoy", ct: ct);
        }

        foreach (var (chatId, hitosDelChat) in resultado.HitosPorChat)
        {
            await telegram.EnviarMensajeAsync(chatId, $"📅 Recordatorio - {DateTime.Now:dd/MM/yyyy}", ct: ct);

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

                var envio = await telegram.EnviarMensajeAsync(chatId, $"- {hito.HitoTexto}", teclado, ct);

                if (envio is { Success: true, MessageId: { } messageId })
                {
                    await hitosRepo.GuardarEnvioAsync(hito.Id, messageId.ToString(), DateOnly.FromDateTime(DateTime.Now), ct);
                    Logger.LogInformation("EnvioDiarioJob: hito {HitoId} enviado a chat {ChatId} (mensaje {MessageId}).", hito.Id, chatId, messageId);
                }
                else
                {
                    Logger.LogWarning("No se pudo enviar/guardar el hito {HitoId} al chat {ChatId}.", hito.Id, chatId);
                }
            }
        }
    }
}
