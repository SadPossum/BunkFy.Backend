namespace BunkFy.Modules.Ingestion.Tests.Api;

using BunkFy.Modules.Ingestion.Api;
using Gma.Framework.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionAdapterIngressOptionsValidatorTests
{
    [Fact]
    public void Ingress_is_disabled_when_configuration_is_absent()
    {
        Assert.False(new IngestionAdapterIngressOptions().Enabled);
    }

    [Fact]
    public void Production_allows_disabled_ingress_without_a_distributed_provider()
    {
        ValidateOptionsResult result = CreateValidator(
            Environments.Production,
            isDistributed: false).Validate(
                name: null,
                new IngestionAdapterIngressOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Production_rejects_enabled_ingress_without_a_distributed_provider()
    {
        ValidateOptionsResult result = CreateValidator(
            Environments.Production,
            isDistributed: false).Validate(
                name: null,
                new IngestionAdapterIngressOptions { Enabled = true });

        Assert.True(result.Failed);
        Assert.Contains("distributed quota provider", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_allows_enabled_ingress_with_a_distributed_provider()
    {
        ValidateOptionsResult result = CreateValidator(
            Environments.Production,
            isDistributed: true).Validate(
                name: null,
                new IngestionAdapterIngressOptions { Enabled = true });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Development_can_enable_ingress_for_local_testing()
    {
        ValidateOptionsResult result = CreateValidator(
            Environments.Development,
            isDistributed: false).Validate(
                name: null,
                new IngestionAdapterIngressOptions { Enabled = true });

        Assert.True(result.Succeeded);
    }

    private static IngestionAdapterIngressOptionsValidator CreateValidator(
        string environmentName,
        bool isDistributed) =>
        new(
            new TestHostEnvironment { EnvironmentName = environmentName },
            new TestQuotaProviderRegistration(isDistributed));

    private sealed class TestQuotaProviderRegistration(bool isDistributed)
        : IRateLimitProviderRegistration
    {
        public bool IsDistributed { get; } = isDistributed;
        public string ProviderName => this.IsDistributed ? "distributed-test" : "local-test";
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "BunkFy.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
