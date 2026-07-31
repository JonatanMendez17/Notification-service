using Microsoft.AspNetCore.Authentication;

namespace Notification.Api.Infrastructure.Authentication;

public class AuthOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
}
