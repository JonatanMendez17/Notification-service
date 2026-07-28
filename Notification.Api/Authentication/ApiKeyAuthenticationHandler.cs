using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Notification.Api.Models.Response;
using Notification.Api.Providers;
using Notification.Api.Settings;

namespace Notification.Api.Authentication;

// Autenticación centralizada de la API: se aplica a nivel de pipeline (Program.cs)
// en vez de dentro de cada servicio, para que ningún endpoint nuevo quede desprotegido
// por olvido. El contrato de respuesta 401 se mantiene igual al que documenta el README.
public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    IOptions<ApiSettings> apiSettings,
    INotificationProvider provider)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(options, loggerFactory, encoder)
{
    private readonly ApiSettings _apiSettings = apiSettings.Value;
    private readonly INotificationProvider _provider = provider;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtraerToken(Request.Headers.Authorization.ToString());

        // Sin token: caso normal (ej. /health, que no requiere auth) — UseAuthentication()
        // igual intenta autenticar todas las requests, así que esto pasa en cada poll de un
        // monitor. No es un intento de acceso real, no vale la pena loguearlo.
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Token de autorización inválido."));
        }

        // Con token pero incorrecto: esto sí es un intento de acceso genuino (o un cliente
        // mal configurado) — vale la pena que quede en el log.
        if (!string.Equals(token, _apiSettings.TokenBearer, StringComparison.Ordinal))
        {
            Logger.LogWarning("Intento de acceso a {Path} con token inválido.", Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("Token de autorización inválido."));
        }

        var identity = new ClaimsIdentity(ApiKeyAuthenticationOptions.SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), ApiKeyAuthenticationOptions.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new EnviarMensajeResponse
        {
            Exitoso = false,
            Mensaje = "Token de autorización inválido.",
            Canal = _provider.Canal,
            Timestamp = DateTime.UtcNow
        });
    }

    private static string ExtraerToken(string authHeader) => authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? authHeader["Bearer ".Length..].Trim()
        : string.Empty;
}
