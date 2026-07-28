namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Modules.Ingestion.Application.Ingress;
using Gma.Framework.RateLimiting;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterIngressQuotaOptionsTests
{
    [Fact]
    public void Defaults_are_valid()
    {
        Assert.True(AdapterIngressQuotaOptions.IsValid(new AdapterIngressQuotaOptions()));
    }

    [Theory]
    [InlineData(0, 2_000, 1_000, 20_000)]
    [InlineData(121, 120, 1_000, 20_000)]
    [InlineData(121, 2_000, 120, 20_000)]
    [InlineData(120, 2_001, 1_000, 2_000)]
    [InlineData(120, 2_000, 1_000_000_001, 20_000)]
    public void Invalid_or_inconsistent_limits_are_rejected(
        int credentialPerMinute,
        int credentialPerHour,
        int tenantPerMinute,
        int tenantPerHour)
    {
        var options = new AdapterIngressQuotaOptions
        {
            CredentialPerMinute = credentialPerMinute,
            CredentialPerHour = credentialPerHour,
            TenantPerMinute = tenantPerMinute,
            TenantPerHour = tenantPerHour
        };

        Assert.False(AdapterIngressQuotaOptions.IsValid(options));
    }

    [Fact]
    public void Framework_maximum_is_accepted_when_relationships_are_consistent()
    {
        int maximum = FixedWindowRateLimitPartition.PermitLimitMaxValue;
        var options = new AdapterIngressQuotaOptions
        {
            CredentialPerMinute = maximum,
            CredentialPerHour = maximum,
            TenantPerMinute = maximum,
            TenantPerHour = maximum
        };

        Assert.True(AdapterIngressQuotaOptions.IsValid(options));
    }
}
