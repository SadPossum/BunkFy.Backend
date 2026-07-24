namespace Integration.Tests;

using BunkFy.Host.Api.Security;
using Gma.Framework.Security;
using Microsoft.Extensions.Configuration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class BunkFyAuthenticationAssuranceTests
{
    [Fact]
    public void Privileged_operations_require_the_configured_fresh_authentication_window()
    {
        ConfigurationManager configuration = new();
        configuration[BunkFyAuthenticationAssurance.PrivilegedOperationFreshnessMinutesKey] = "10";

        AuthenticationAssuranceRequirement requirement =
            BunkFyAuthenticationAssurance.CreatePrivilegedOperationRequirement(configuration);

        Assert.Equal(TimeSpan.FromMinutes(10), requirement.MaxAuthenticationAge);
        Assert.Empty(requirement.AcceptedContextReferences);
    }

    [Fact]
    public void Destructive_operations_require_recent_multi_factor_authentication()
    {
        ConfigurationManager configuration = new();
        configuration[BunkFyAuthenticationAssurance.PrivilegedOperationFreshnessMinutesKey] = "10";

        AuthenticationAssuranceRequirement requirement =
            BunkFyAuthenticationAssurance.CreateDestructiveOperationRequirement(configuration);

        Assert.Equal(TimeSpan.FromMinutes(10), requirement.MaxAuthenticationAge);
        Assert.Equal(
            [
                BunkFyAuthenticationAssurance.MultiFactorContextReference,
                BunkFyAuthenticationAssurance.TwoStepContextReference
            ],
            requirement.AcceptedContextReferences);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("61")]
    [InlineData("ten")]
    public void Invalid_privileged_operation_freshness_is_rejected(string? configured)
    {
        ConfigurationManager configuration = new();
        configuration[BunkFyAuthenticationAssurance.PrivilegedOperationFreshnessMinutesKey] = configured;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            BunkFyAuthenticationAssurance.CreatePrivilegedOperationRequirement(configuration));

        Assert.Contains(
            BunkFyAuthenticationAssurance.PrivilegedOperationFreshnessMinutesKey,
            exception.Message,
            StringComparison.Ordinal);
    }
}
