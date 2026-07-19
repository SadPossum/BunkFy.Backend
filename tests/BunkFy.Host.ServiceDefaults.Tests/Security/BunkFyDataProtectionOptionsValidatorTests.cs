namespace BunkFy.Host.ServiceDefaults.Tests.Security;

using BunkFy.Host.ServiceDefaults.Security;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class BunkFyDataProtectionOptionsValidatorTests
{
    private readonly BunkFyDataProtectionOptionsValidator validator = new();

    [Fact]
    public void Validate_rejects_required_persistence_without_a_key_ring_path()
    {
        BunkFyDataProtectionOptions options = new()
        {
            RequirePersistentKeys = true,
            ApplicationName = "bunkfy"
        };

        Microsoft.Extensions.Options.ValidateOptionsResult result = this.validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("KeyRingPath", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_accepts_a_persistent_key_ring_configuration()
    {
        BunkFyDataProtectionOptions options = new()
        {
            RequirePersistentKeys = true,
            KeyRingPath = "data/data-protection",
            ApplicationName = "bunkfy"
        };

        Microsoft.Extensions.Options.ValidateOptionsResult result = this.validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Production_composition_rejects_an_ephemeral_key_ring()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
        });
        builder.Configuration["DataProtection:ApplicationName"] = "bunkfy";

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            builder.AddBunkFyDataProtection());

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("Production", StringComparison.Ordinal));
    }
}
