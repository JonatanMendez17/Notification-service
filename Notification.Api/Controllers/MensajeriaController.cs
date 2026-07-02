using Microsoft.AspNetCore.Mvc;
using Notification.Api.Models.Request;
using Notification.Api.Models.Response;
using Notification.Api.Services;

namespace Notification.Api.Controllers;

[ApiController]
[Route("api/mensajeria")]
[Produces("application/json")]
public class MensajeriaController : ControllerBase
{
    private readonly IMensajeriaService _mensajeriaService;
    private readonly ILogger<MensajeriaController> _logger;

    public MensajeriaController(IMensajeriaService mensajeriaService, ILogger<MensajeriaController> logger)
    {
        _mensajeriaService = mensajeriaService;
        _logger = logger;
    }

    /// <summary>
    /// Envía un mensaje formateado a través de Telegram.
    /// </summary>
    /// <param name="request">Datos del mensaje a enviar.</param>
    /// <returns>Resultado del envío.</returns>
    [HttpPost("enviarMsgTG")]
    [ProducesResponseType(typeof(EnviarMensajeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(EnviarMensajeResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(EnviarMensajeResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> EnviarMsgTG([FromBody] EnviarMensajeRequest request)
    {
        var token = ExtraerToken(Request.Headers.Authorization.ToString());

        try
        {
            var resultado = await _mensajeriaService.EnviarTelegramAsync(request, token);

            if (!resultado.Exitoso && resultado.Mensaje.Contains("Token"))
                return Unauthorized(resultado);

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Endpoint}", nameof(EnviarMsgTG));
            return StatusCode(StatusCodes.Status500InternalServerError, new EnviarMensajeResponse
            {
                Exitoso = false,
                Mensaje = "Error interno del servidor.",
                Canal = "Telegram",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private static string ExtraerToken(string authHeader) =>
        authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : string.Empty;
}
