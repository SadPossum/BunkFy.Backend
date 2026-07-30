namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Contributors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffRetentionConfigurationTests
{
    [Fact]
    public void Defaults_are_bounded_and_valid()
    {
        StaffRetentionOptions options = new();

        ValidateOptionsResult validation =
            new StaffRetentionOptionsValidator().Validate(
                null,
                options);

        Assert.True(validation.Succeeded);
        Assert.Equal(60, options.IntervalMinutes);
        Assert.Equal(100, options.ScanSize);
        Assert.Equal(25, options.MutationBatchSize);
    }

    [Fact]
    public void Mutation_batch_cannot_exceed_scan_size()
    {
        StaffRetentionOptions options = new()
        {
            ScanSize = 10,
            MutationBatchSize = 11
        };

        ValidateOptionsResult validation =
            new StaffRetentionOptionsValidator().Validate(
                null,
                options);

        Assert.True(validation.Failed);
        Assert.Contains(
            validation.Failures,
            failure => failure.Contains(
                "cannot exceed ScanSize",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Application_registration_is_idempotent()
    {
        ServiceCollection services = new();

        services.AddStaffApplication();
        services.AddStaffApplication();

        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(IRetentionExecutionContributor) &&
                descriptor.ImplementationType ==
                    typeof(StaffRetentionContributor));
        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                    typeof(IValidateOptions<StaffRetentionOptions>) &&
                descriptor.ImplementationType ==
                    typeof(StaffRetentionOptionsValidator));
    }
}
