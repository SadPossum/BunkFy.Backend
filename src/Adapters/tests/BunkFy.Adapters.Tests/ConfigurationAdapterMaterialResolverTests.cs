namespace BunkFy.Adapters.Tests;

using System.Text;
using BunkFy.Adapters.Configuration;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Contracts.Adapters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ConfigurationAdapterMaterialResolverTests
{
    [Fact]
    public async Task Resolves_versioned_configuration_and_secret_from_opaque_references()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Adapters:Materials:Configurations:fake-main:SchemaVersion"] = "1",
            ["Adapters:Materials:Configurations:fake-main:ContentType"] = "application/json",
            ["Adapters:Materials:Configurations:fake-main:Value"] = "{\"endpoint\":\"https://example.test/feed\"}",
            ["Adapters:Materials:Secrets:fake-main:ContentType"] = "application/json",
            ["Adapters:Materials:Secrets:fake-main:Value"] = "{\"authorizationHeaderValue\":\"Bearer private\"}"
        });
        ServiceProvider provider = CreateProvider(configuration);
        IAdapterConfigurationMaterialResolver resolver = provider.GetRequiredService<IAdapterConfigurationMaterialResolver>();

        Result<BunkFy.Adapter.Abstractions.AdapterConfigurationMaterial> result = await resolver.ResolveAsync(
            CreateRequest(schemaVersion: 1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        using BunkFy.Adapter.Abstractions.AdapterConfigurationMaterial material = result.Value;
        Assert.Contains("example.test", Encoding.UTF8.GetString(material.Configuration.Span), StringComparison.Ordinal);
        Assert.Contains("Bearer private", Encoding.UTF8.GetString(material.Secret.Span), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_schema_mismatch_without_returning_material()
    {
        IConfiguration configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Adapters:Materials:Configurations:fake-main:SchemaVersion"] = "2",
            ["Adapters:Materials:Configurations:fake-main:Value"] = "{}"
        });
        IAdapterConfigurationMaterialResolver resolver = CreateProvider(configuration)
            .GetRequiredService<IAdapterConfigurationMaterialResolver>();

        Result<BunkFy.Adapter.Abstractions.AdapterConfigurationMaterial> result = await resolver.ResolveAsync(
            CreateRequest(schemaVersion: 1) with { SecretReference = null },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AdapterConfigurationMaterialErrors.SchemaMismatch, result.Error);
    }

    [Theory]
    [InlineData("https://not-a-reference")]
    [InlineData("configuration://../escape")]
    [InlineData("configuration://name/child")]
    public async Task Rejects_invalid_configuration_references(string reference)
    {
        IAdapterConfigurationMaterialResolver resolver = CreateProvider(BuildConfiguration([]))
            .GetRequiredService<IAdapterConfigurationMaterialResolver>();

        Result<BunkFy.Adapter.Abstractions.AdapterConfigurationMaterial> result = await resolver.ResolveAsync(
            CreateRequest(schemaVersion: 1) with { ConfigurationReference = reference, SecretReference = null },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AdapterConfigurationMaterialErrors.ReferenceInvalid, result.Error);
    }

    private static AdapterConfigurationMaterialRequest CreateRequest(int schemaVersion) => new(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        "fake.http",
        schemaVersion,
        "configuration://fake-main",
        "secret://fake-main");

    private static IConfiguration BuildConfiguration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceProvider CreateProvider(IConfiguration configuration)
    {
        ServiceCollection services = new();
        services.AddLocalAdapterConfigurationMaterials(configuration);
        return services.BuildServiceProvider();
    }
}
