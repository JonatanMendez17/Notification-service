using Microsoft.Extensions.Options;
using Notification.Api.Models.Request;
using Notification.Api.Models.Response;
using Notification.Api.Providers;
using Notification.Api.Settings;

namespace Notification.Api.Services;

public class MensajeriaService : IMensajeriaService
{
    private readonly INotificationProvider _telegramProvider;
    private readonly ApiSettings _apiSettings;
    private readonly ILogger<MensajeriaService> _logger;

    public MensajeriaService(TelegramProvider telegramProvider, IOptions<ApiSettings> apiSettings, ILogger<MensajeriaService> logger)
    {
        _telegramProvider = telegramProvider;
        _apiSettings = apiSettings.Value;
        _logger = logger;
    }

    public async Task<EnviarMensajeResponse> EnviarTelegramAsync(EnviarMensajeRequest request)
    {
        if (!TokenEsValido(request.TokenBearer))
        {
            _logger.LogWarning("Intento de acceso con token inválido. Sistema: {Sistema}", request.Sistema);
            return Respuesta(false, "Token de autorización inválido.", _telegramProvider.Canal);
        }

        var texto = ConstruirTexto(request);

        _logger.LogInformation("Enviando mensaje por {Canal}. Sistema: {Sistema}", _telegramProvider.Canal, request.Sistema);

        var enviado = await _telegramProvider.EnviarAsync(texto);

        return enviado
            ? Respuesta(true, "Mensaje enviado correctamente.", _telegramProvider.Canal)
            : Respuesta(false, "Error al enviar el mensaje. Intente nuevamente.", _telegramProvider.Canal);
    }

    private bool TokenEsValido(string token) => !string.IsNullOrWhiteSpace(token) && string.Equals(token, _apiSettings.TokenBearer, StringComparison.Ordinal);

    private static string ConstruirTexto(EnviarMensajeRequest r) => 
        $"De: {r.De}\n" +
        $"Para: {r.Para}\n" +
        $"{r.Titulo}\n\n" +
        $"{r.Mensaje}";

    private static EnviarMensajeResponse Respuesta(bool exitoso, string mensaje, string canal) => new() { Exitoso = exitoso, Mensaje = mensaje, Canal = canal, Timestamp = DateTime.UtcNow };
}
