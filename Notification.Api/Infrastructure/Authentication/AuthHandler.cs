using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Notification.Api.Infrastructure.Settings;
using Notification.Api.Models.Response;

namespace Notification.Api.Infrastructure.Authentication;

// Autenticación centralizada de la API
public class AuthHandler(IOptionsMonitor<AuthOptions> options, ILoggerFactory loggerFactory, UrlEncoder encoder, IOptions<ApiSettings> apiSettings) : AuthenticationHandler<AuthOptions>(options, loggerFactory, encoder)
{
    private readonly ApiSettings _apiSettings = apiSettings.Value;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ExtraerToken(Request.Headers.Authorization.ToString());

        // Sin token: caso normal. Intenta autenticar todas las requests, así que esto pasa en cada poll de un monitor
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Token de autorización inválido."));
        }

        // Con token pero incorrecto: Que quede en el log.
        if (!string.Equals(token, _apiSettings.TokenBearer, StringComparison.Ordinal))
        {
            Logger.LogWarning("Intento de acceso a {Path} con token inválido.", Request.Path);
            return Task.FromResult(AuthenticateResult.Fail("Token de autorización inválido."));
        }

        var identity = new ClaimsIdentity(AuthOptions.SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), AuthOptions.SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        await Response.WriteAsJsonAsync(new EnviarMensajeResponse
        {
            Exitoso = false,
            Mensaje = "Token de autorización inválido.",
            Canal = string.Empty,
            Timestamp = DateTime.UtcNow
        });
    }

    private static string ExtraerToken(string authHeader) => authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
        ? authHeader["Bearer ".Length..].Trim()
        : string.Empty;
}
