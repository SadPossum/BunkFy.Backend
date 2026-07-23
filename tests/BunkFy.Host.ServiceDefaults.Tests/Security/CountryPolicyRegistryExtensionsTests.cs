namespace BunkFy.Host.ServiceDefaults.Tests.Security;

using BunkFy.DataGovernance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CountryPolicyRegistryExtensionsTests
{
    [Fact]
    public void Empty_configuration_registers_a_fail_closed_registry()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();

        builder.AddBunkFyCountryPolicies();

        using ServiceProvider provider = builder.Services.BuildServiceProvider();
        CountryPolicyRegistry registry = provider.GetRequiredService<CountryPolicyRegistry>();
        Assert.Empty(registry.ListPolicies());
    }

    [Fact]
    public void Configured_pack_directory_must_exist()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            HostApplicationBuilder builder = CreateBuilder(root);
            builder.Configuration[$"{CountryPolicyRegistryExtensions.ConfigurationSection}:PackDirectory"] =
                "missing";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                builder.AddBunkFyCountryPolicies);

            Assert.Contains("does not exist", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Oversized_pack_is_rejected_before_its_contents_are_read()
    {
        string root = CreateTemporaryDirectory();
        string packs = Directory.CreateDirectory(Path.Combine(root, "packs")).FullName;
        File.WriteAllBytes(
            Path.Combine(packs, "oversized.json"),
            new byte[CountryPolicyPackJson.MaximumDocumentBytes + 1]);
        try
        {
            HostApplicationBuilder builder = CreateBuilder(root);
            builder.Configuration[$"{CountryPolicyRegistryExtensions.ConfigurationSection}:PackDirectory"] = "packs";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                builder.AddBunkFyCountryPolicies);

            Assert.Contains("must contain between 1", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Pack_directory_has_a_bounded_file_count()
    {
        string root = CreateTemporaryDirectory();
        string packs = Directory.CreateDirectory(Path.Combine(root, "packs")).FullName;
        for (int index = 0; index <= CountryPolicyRegistryExtensions.MaximumPackFiles; index++)
        {
            File.WriteAllText(Path.Combine(packs, $"pack-{index:D3}.json"), "{}");
        }

        try
        {
            HostApplicationBuilder builder = CreateBuilder(root);
            builder.Configuration[$"{CountryPolicyRegistryExtensions.ConfigurationSection}:PackDirectory"] = "packs";

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                builder.AddBunkFyCountryPolicies);

            Assert.Contains("cannot contain more than", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static HostApplicationBuilder CreateBuilder(string contentRootPath) =>
        Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = contentRootPath,
            EnvironmentName = Environments.Production
        });

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"bunkfy-country-policies-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
