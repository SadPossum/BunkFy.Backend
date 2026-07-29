namespace BunkFy.Modules.Guests.Tests.Application;

using BunkFy.Modules.Guests.Application;
using BunkFy.Modules.Guests.Application.Contributors;
using BunkFy.Modules.Retention.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestRetentionConfigurationTests
{
    [Fact]
    public void Defaults_are_bounded_and_valid()
    {
        GuestRetentionOptions options = new();

        ValidateOptionsResult validation =
            new GuestRetentionOptionsValidator().Validate(null, options);

        Assert.True(validation.Succeeded);
        Assert.Equal(60, options.IntervalMinutes);
        Assert.Equal(100, options.ScanSize);
        Assert.Equal(25, options.MutationBatchSize);
    }

    [Fact]
    public void Mutation_batch_cannot_exceed_scan_size()
    {
        GuestRetentionOptions options = new()
        {
            ScanSize = 10,
            MutationBatchSize = 11
        };

        ValidateOptionsResult validation =
            new GuestRetentionOptionsValidator().Validate(null, options);

        Assert.True(validation.Failed);
        Assert.Contains(
            validation.Failures,
            failure => failure.Contains(
                "cannot exceed ScanSize",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Application_registration_adds_one_retention_contributor()
    {
        ServiceCollection services = new();

        services.AddGuestsApplication();
        services.AddGuestsApplication();

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(IRetentionExecutionContributor) &&
                descriptor.ImplementationType ==
                    typeof(GuestRetentionContributor));
        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(IValidateOptions<GuestRetentionOptions>) &&
                descriptor.ImplementationType ==
                    typeof(GuestRetentionOptionsValidator));
    }
}
