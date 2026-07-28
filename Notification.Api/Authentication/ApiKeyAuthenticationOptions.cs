using Microsoft.AspNetCore.Authentication;

namespace Notification.Api.Authentication;

public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}
