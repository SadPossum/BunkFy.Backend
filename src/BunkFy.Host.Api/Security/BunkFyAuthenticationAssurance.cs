namespace BunkFy.Host.Api.Security;

using Gma.Framework.Security;
using Microsoft.Extensions.Configuration;

internal static class BunkFyAuthenticationAssurance
{
    public const string MultiFactorContextReference = "urn:gma:acr:mfa";
    public const string TwoStepContextReference = "urn:gma:acr:two-step";
    public const string PrivilegedOperationFreshnessMinutesKey =
        "BunkFy:AuthenticationAssurance:PrivilegedOperationFreshnessMinutes";

    public static AuthenticationAssuranceRequirement CreatePrivilegedOperationRequirement(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AuthenticationAssuranceRequirement(
            maxAuthenticationAge: ReadFreshness(configuration));
    }

    public static AuthenticationAssuranceRequirement CreateDestructiveOperationRequirement(
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AuthenticationAssuranceRequirement(
            [MultiFactorContextReference, TwoStepContextReference],
            ReadFreshness(configuration));
    }

    private static TimeSpan ReadFreshness(IConfiguration configuration)
    {
        string? configured = configuration[PrivilegedOperationFreshnessMinutesKey];
        if (!int.TryParse(configured, out int freshnessMinutes) || freshnessMinutes is < 1 or > 60)
        {
            throw new InvalidOperationException(
                $"{PrivilegedOperationFreshnessMinutesKey} must be a whole number from 1 through 60.");
        }

        return TimeSpan.FromMinutes(freshnessMinutes);
    }
}
