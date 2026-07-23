using Notification.Engine.Data;

namespace Notification.Engine.Telegram;

// Workflow 2 de N8N ("Respuesta y Registro"). Ver mapeo detallado en
// Recursos\plan-etapas-desarrollo.md. NO es un Job — no tiene timer propio,
// reacciona a lo que le pasa el receiver compartido (PollingReceiver/WebhookReceiver).
public class RespuestaRegistroHandler
{
    private static readonly string[] PalabrasRegistro = ["registro", "registrar", "registrarme"];
    private static readonly string[] TiposChatValidos = ["group", "supergroup", "channel"];

    private readonly IHitosRepository _hitosRepo;
    private readonly IGruposRepository _gruposRepo;
    private readonly TelegramBotClient _telegram;
    private readonly ILogger<RespuestaRegistroHandler> _logger;

    public RespuestaRegistroHandler(
        IHitosRepository hitosRepo,
        IGruposRepository gruposRepo,
        TelegramBotClient telegram,
        ILogger<RespuestaRegistroHandler> logger)
    {
        _hitosRepo = hitosRepo;
        _gruposRepo = gruposRepo;
        _telegram = telegram;
        _logger = logger;
    }

    public async Task OnCallbackQueryAsync(TelegramCallbackQuery cq, CancellationToken ct)
    {
        await _telegram.AnswerCallbackQueryAsync(cq.Id, ct);

        var partes = (cq.Data ?? string.Empty).Split('|');
        if (partes.Length != 2 || !int.TryParse(partes[1], out var hitoId))
        {
            _logger.LogWarning("callback_data inválido: {Data}", cq.Data);
            return;
        }

        var accion = partes[0];
        var chatId = cq.Message.Chat.Id.ToString();
        var messageId = cq.Message.MessageId.ToString();
        var hitoTexto = ExtraerHitoDelTexto(cq.Message.Text);
        var tgUserId = cq.From.Id;
        var nombreCompleto = $"{cq.From.FirstName} {cq.From.LastName}".Trim();

        if (accion == "ok")
        {
            await _telegram.EditMessageAsync(chatId, messageId, $"✅OK-{hitoTexto}", ct);
            await _hitosRepo.RegistrarHitoOkAsync(hitoId, tgUserId, nombreCompleto, ct);
            _logger.LogInformation("RespuestaRegistroHandler: hito {HitoId} marcado OK por {Nombre}.", hitoId, nombreCompleto);
        }
        else if (accion.StartsWith("posponer"))
        {
            var sufijo = accion["posponer".Length..];
            var dias = sufijo.Length > 0 && int.TryParse(sufijo, out var n) ? n : 1;
            var nuevaFecha = DateOnly.FromDateTime(DateTime.Now.AddDays(dias));

            await _telegram.EditMessageAsync(chatId, messageId, $"⏰Pospuesto-{hitoTexto}", ct);
            await _hitosRepo.RegistrarHitoPospuestoAsync(hitoId, nuevaFecha, $"Posponer +{dias}", tgUserId, nombreCompleto, ct);
            _logger.LogInformation("RespuestaRegistroHandler: hito {HitoId} pospuesto a {Fecha} por {Nombre}.", hitoId, nuevaFecha, nombreCompleto);
        }
        else
        {
            _logger.LogWarning("Acción de callback desconocida: {Accion}", accion);
            return;
        }

        var treId = await _gruposRepo.UpsertReceptorAsync(tgUserId, cq.From.FirstName ?? string.Empty, cq.From.LastName ?? string.Empty, ct);
        var tggId = await _gruposRepo.GetTggIdPorChatIdAsync(chatId, ct);
        if (tggId is not null)
        {
            await _gruposRepo.AsegurarGrupoReceptorAsync(tggId.Value, treId, ct);
        }
    }

    public async Task OnMessageAsync(TelegramIncomingMessage msg, CancellationToken ct)
    {
        if (msg.Text is null || !TiposChatValidos.Contains(msg.Chat.Type)) return;

        var primeraParte = msg.Text.Trim().Split(' ')[0].Split('@')[0];
        var comando = primeraParte.StartsWith('/') ? primeraParte[1..].ToLowerInvariant() : primeraParte.ToLowerInvariant();
        if (!PalabrasRegistro.Contains(comando)) return;

        var chatId = msg.Chat.Id.ToString();

        if (await _gruposRepo.ExisteGrupoAsync(chatId, ct))
        {
            await _telegram.SendMessageAsync(chatId, "⚠️ Este grupo ya está registrado en el sistema.", ct: ct);
            return;
        }

        var ownerNombre = msg.From is null ? null : $"{msg.From.FirstName} {msg.From.LastName}".Trim();

        await _gruposRepo.RegistrarGrupoAsync(
            msg.Chat.Title ?? "Grupo sin nombre",
            chatId,
            msg.From?.Id,
            string.IsNullOrWhiteSpace(ownerNombre) ? null : ownerNombre,
            msg.From?.Username,
            ct);

        await _telegram.SendMessageAsync(
            chatId,
            "✅ *Grupo registrado y activado correctamente.*\n\n¡Ya podés comenzar a recibir recordatorios!",
            ct: ct);

        _logger.LogInformation("RespuestaRegistroHandler: grupo {ChatId} registrado.", chatId);
    }

    // El mensaje original tiene el formato "- {hito}" (ver EnvioDiarioJob).
    private static string ExtraerHitoDelTexto(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        return texto.StartsWith("- ") ? texto[2..] : texto;
    }
}
