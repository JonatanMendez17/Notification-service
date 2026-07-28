using Notification.Api.Models.Request;
using Notification.Api.Models.Response;
using Notification.Api.Providers;

namespace Notification.Api.Services;

public class MensajeriaService(INotificationProvider provider, ILogger<MensajeriaService> logger) : IMensajeriaService
{
    private readonly INotificationProvider _provider = provider;
    private readonly ILogger<MensajeriaService> _logger = logger;

    public async Task<EnviarMensajeResponse> EnviarAsync(EnviarMensajeRequest request)
    {
        var texto = ConstruirTexto(request);

        _logger.LogInformation("Enviando mensaje por {Canal}. Sistema: {Sistema}", _provider.Canal, request.Sistema);

        var enviado = await _provider.EnviarAsync(texto);

        return enviado
            ? Respuesta(true, "Mensaje enviado correctamente.", _provider.Canal)
            : Respuesta(false, "Error al enviar el mensaje. Intente nuevamente.", _provider.Canal);
    }

    private static string ConstruirTexto(EnviarMensajeRequest r) =>
        $"De: {r.De}\n" +
        $"Para: {r.Para}\n" +
        $"{r.Titulo}\n\n" +
        $"{r.Mensaje}";

    private static EnviarMensajeResponse Respuesta(bool exitoso, string mensaje, string canal) => new() { 
        Exitoso = exitoso, 
        Mensaje = mensaje, 
        Canal = canal, 
        Timestamp = DateTime.UtcNow 
    };
}
