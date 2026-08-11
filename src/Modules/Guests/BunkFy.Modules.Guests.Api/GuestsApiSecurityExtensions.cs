namespace BunkFy.Modules.Guests.Api;

using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Microsoft.AspNetCore.Builder;

internal static class GuestsApiSecurityExtensions
{
    public static RouteHandlerBuilder RequireAssuranceWhenConfigured(
        this RouteHandlerBuilder endpoint,
        AuthenticationAssuranceRequirement? requirement) =>
        requirement is null
            ? endpoint
            : endpoint.RequireAuthenticationAssurance(requirement);
}
