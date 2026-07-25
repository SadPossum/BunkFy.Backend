namespace BunkFy.Modules.DataRights.Tests.Persistence;

using System.Text;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using Gma.Framework.Results;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsRecordPseudonymizerTests
{
    [Fact]
    public void Pseudonym_is_deterministic_and_separated_by_tenant_record_and_key_version()
    {
        DataRightsPseudonymisationOptions options = new()
        {
            ActiveKeyVersion = 1,
            Keys =
            {
                [1] = Key('a'),
                [2] = Key('b')
            }
        };
        HmacDataRightsRecordPseudonymizer pseudonymizer =
            new(Options.Create(options));
        Guid recordId = Guid.NewGuid();

        DataRightsRecordPseudonym first = pseudonymizer.CreateActive(
            "tenant-a",
            "guests",
            "guest-profile",
            recordId).Value;
        DataRightsRecordPseudonym equivalent = pseudonymizer.CreateActive(
            " tenant-a ",
            " guests ",
            " guest-profile ",
            recordId).Value;
        DataRightsRecordPseudonym anotherTenant = pseudonymizer.CreateActive(
            "tenant-b",
            "guests",
            "guest-profile",
            recordId).Value;
        DataRightsRecordPseudonym rotated = pseudonymizer.Create(
            2,
            "tenant-a",
            "guests",
            "guest-profile",
            recordId).Value;

        Assert.Equal(first, equivalent);
        Assert.NotEqual(first.Sha256, anotherTenant.Sha256);
        Assert.NotEqual(first.Sha256, rotated.Sha256);
        Assert.Equal(1, first.KeyVersion);
        Assert.Equal(2, rotated.KeyVersion);
    }

    [Fact]
    public void Missing_key_version_and_invalid_coordinate_fail_without_a_digest()
    {
        HmacDataRightsRecordPseudonymizer pseudonymizer = new(Options.Create(
            new DataRightsPseudonymisationOptions
            {
                ActiveKeyVersion = 1,
                Keys = { [1] = Key('a') }
            }));

        Result<DataRightsRecordPseudonym> missing = pseudonymizer.Create(
            2,
            "tenant-a",
            "guests",
            "guest-profile",
            Guid.NewGuid());
        Result<DataRightsRecordPseudonym> invalid = pseudonymizer.CreateActive(
            "tenant-a",
            "guests",
            "guest-profile",
            Guid.Empty);

        Assert.Equal("DataRights.RecordPseudonymKeyUnavailable", missing.Error.Code);
        Assert.Equal("DataRights.RecordPseudonymInvalid", invalid.Error.Code);
    }

    [Fact]
    public void Production_validation_rejects_development_and_duplicate_key_material()
    {
        DataRightsPseudonymisationOptions development = new()
        {
            ActiveKeyVersion = 1,
            Keys =
            {
                [1] = DataRightsPseudonymisationOptions.DevelopmentKeyBase64
            }
        };
        DataRightsPseudonymisationOptions duplicate = new()
        {
            ActiveKeyVersion = 2,
            Keys =
            {
                [1] = Key('a'),
                [2] = Key('a')
            }
        };

        Assert.True(
            new DataRightsPseudonymisationOptionsValidator(isProduction: true)
                .Validate(name: null, development)
                .Failed);
        Assert.True(
            new DataRightsPseudonymisationOptionsValidator(isProduction: false)
                .Validate(name: null, development)
                .Succeeded);
        Assert.True(
            new DataRightsPseudonymisationOptionsValidator(isProduction: true)
                .Validate(name: null, duplicate)
                .Failed);
    }

    [Fact]
    public void Production_validation_accepts_distinct_deployment_keys_and_active_version()
    {
        DataRightsPseudonymisationOptions configured = new()
        {
            ActiveKeyVersion = 2,
            Keys =
            {
                [1] = Key('a'),
                [2] = Key('b')
            }
        };

        Assert.True(
            new DataRightsPseudonymisationOptionsValidator(isProduction: true)
                .Validate(name: null, configured)
                .Succeeded);
    }

    [Fact]
    public void Production_registration_fails_closed_without_deployment_key()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Production
            });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] =
                "Host=localhost;Database=unused;Username=unused;Password=unused"
        });
        builder.AddDataRightsPersistence();
        using IHost host = builder.Build();

        OptionsValidationException failure =
            Assert.Throws<OptionsValidationException>(
                () => host.Services
                    .GetRequiredService<IOptions<DataRightsPseudonymisationOptions>>()
                    .Value);

        Assert.Contains(
            failure.Failures,
            message => message.Contains(":Keys must contain", StringComparison.Ordinal));
    }

    [Fact]
    public void Partial_non_production_registration_does_not_use_the_development_fallback()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Staging
            });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] =
                "Host=localhost;Database=unused;Username=unused;Password=unused",
            [$"{DataRightsPseudonymisationOptions.SectionName}:ActiveKeyVersion"] = "2"
        });
        builder.AddDataRightsPersistence();
        using IHost host = builder.Build();

        OptionsValidationException failure =
            Assert.Throws<OptionsValidationException>(
                () => host.Services
                    .GetRequiredService<IOptions<DataRightsPseudonymisationOptions>>()
                    .Value);

        Assert.Contains(
            failure.Failures,
            message => message.Contains(":Keys must contain", StringComparison.Ordinal));
    }

    private static string Key(char value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(new string(value, 32)));
}
