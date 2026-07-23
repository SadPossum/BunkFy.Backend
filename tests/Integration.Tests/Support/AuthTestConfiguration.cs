namespace Integration.Tests.Support;

using Microsoft.Extensions.Configuration;

internal static class AuthTestConfiguration
{
    public const string RefreshTokenPepper =
        "integration-test-refresh-token-pepper-change-me-000000000000000000";

    public static void ConfigureTokenHashing(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration["Auth:RefreshTokens:Pepper"] = RefreshTokenPepper;
    }
}
