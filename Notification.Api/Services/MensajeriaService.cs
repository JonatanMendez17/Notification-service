using Microsoft.Extensions.Options;
using Notification.Api.Models.Request;
using Notification.Api.Models.Response;
using Notification.Api.Providers;
using Notification.Api.Settings;

namespace Notification.Api.Services;

public class MensajeriaService : IMensajeriaService
{
    private readonly INotificationProvider _provider;
    private readonly ApiSettings _apiSettings;
    private readonly ILogger<MensajeriaService> _logger;

    public MensajeriaService(INotificationProvider provider, IOptions<ApiSettings> apiSettings, ILogger<MensajeriaService> logger)
    {
        _provider = provider;
        _apiSettings = apiSettings.Value;
        _logger = logger;
    }

    public async Task<EnviarMensajeResponse> EnviarAsync(EnviarMensajeRequest request, string token)
    {
        if (!TokenEsValido(token))
        {
            _logger.LogWarning("Intento de acceso con token inválido. Sistema: {Sistema}", request.Sistema);
            return Respuesta(false, "Token de autorización inválido.", _provider.Canal);
        }

        var texto = ConstruirTexto(request);

        _logger.LogInformation("Enviando mensaje por {Canal}. Sistema: {Sistema}", _provider.Canal, request.Sistema);

        var enviado = await _provider.EnviarAsync(texto);

        return enviado
            ? Respuesta(true, "Mensaje enviado correctamente.", _provider.Canal)
            : Respuesta(false, "Error al enviar el mensaje. Intente nuevamente.", _provider.Canal);
    }

    private bool TokenEsValido(string token) =>
        !string.IsNullOrWhiteSpace(token) && string.Equals(token, _apiSettings.TokenBearer, StringComparison.Ordinal);

    private static string ConstruirTexto(EnviarMensajeRequest r) =>
        $"De: {r.De}\n" +
        $"Para: {r.Para}\n" +
        $"{r.Titulo}\n\n" +
        $"{r.Mensaje}";

    private static EnviarMensajeResponse Respuesta(bool exitoso, string mensaje, string canal) =>
        new() { Exitoso = exitoso, Mensaje = mensaje, Canal = canal, Timestamp = DateTime.UtcNow };
}
