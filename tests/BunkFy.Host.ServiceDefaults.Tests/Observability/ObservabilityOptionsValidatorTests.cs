namespace BunkFy.Host.ServiceDefaults.Tests;

using Microsoft.Extensions.Options;
using BunkFy.Host.ServiceDefaults.Observability;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ObservabilityOptionsValidatorTests
{
    private readonly ObservabilityOptionsValidator validator = new();

    [Fact]
    public void Validate_accepts_default_settings()
    {
        ValidateOptionsResult result = this.validator.Validate(name: null, new ObservabilityOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("metrics")]
    public void Validate_rejects_invalid_prometheus_endpoint_path(string endpointPath)
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new ObservabilityOptions
            {
                Prometheus = new PrometheusOptions { EndpointPath = endpointPath }
            });

        AssertFailure(result, "EndpointPath");
    }

    [Fact]
    public void Validate_rejects_enabled_otlp_without_signals()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new ObservabilityOptions
            {
                Otlp = new OtlpOptions
                {
                    Enabled = true,
                    ExportMetrics = false,
                    ExportTraces = false,
                    ExportLogs = false
                }
            });

        AssertFailure(result, "at least one signal");
    }

    [Fact]
    public void Validate_rejects_invalid_otlp_endpoint()
    {
        ValidateOptionsResult result = this.validator.Validate(
            name: null,
            new ObservabilityOptions
            {
                Otlp = new OtlpOptions
                {
                    Endpoint = "not-a-uri"
                }
            });

        AssertFailure(result, "Endpoint");
    }

    private static void AssertFailure(ValidateOptionsResult result, string expectedFailure)
    {
        Assert.True(result.Failed);
        Assert.Contains(expectedFailure, result.FailureMessage, StringComparison.Ordinal);
    }
}
