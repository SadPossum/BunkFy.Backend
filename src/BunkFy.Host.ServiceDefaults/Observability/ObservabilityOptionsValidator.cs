namespace BunkFy.Host.ServiceDefaults.Observability;

using Microsoft.Extensions.Options;

internal sealed class ObservabilityOptionsValidator : IValidateOptions<ObservabilityOptions>
{
    public ValidateOptionsResult Validate(string? name, ObservabilityOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Prometheus.EndpointPath))
        {
            return ValidateOptionsResult.Fail("Observability:Prometheus:EndpointPath is required.");
        }

        if (options.Prometheus.EndpointPath[0] != '/')
        {
            return ValidateOptionsResult.Fail("Observability:Prometheus:EndpointPath must start with '/'.");
        }

        if (options.Otlp.Enabled &&
            !options.Otlp.ExportMetrics &&
            !options.Otlp.ExportTraces &&
            !options.Otlp.ExportLogs)
        {
            return ValidateOptionsResult.Fail("Observability:Otlp must export at least one signal when enabled.");
        }

        if (!string.IsNullOrWhiteSpace(options.Otlp.Endpoint) &&
            (!Uri.TryCreate(options.Otlp.Endpoint, UriKind.Absolute, out Uri? endpoint) ||
             endpoint.Scheme is not ("http" or "https")))
        {
            return ValidateOptionsResult.Fail("Observability:Otlp:Endpoint must be an absolute HTTP or HTTPS URI.");
        }

        return ValidateOptionsResult.Success;
    }
}
