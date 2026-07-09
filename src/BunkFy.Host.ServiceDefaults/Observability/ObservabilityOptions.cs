namespace BunkFy.Host.ServiceDefaults.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public PrometheusOptions Prometheus { get; set; } = new();
    public OtlpOptions Otlp { get; set; } = new();
}

public sealed class PrometheusOptions
{
    public bool Enabled { get; set; }
    public string EndpointPath { get; set; } = "/metrics";
}

public sealed class OtlpOptions
{
    public bool Enabled { get; set; }
    public string? Endpoint { get; set; }
    public bool ExportMetrics { get; set; } = true;
    public bool ExportTraces { get; set; } = true;
    public bool ExportLogs { get; set; } = true;
}
