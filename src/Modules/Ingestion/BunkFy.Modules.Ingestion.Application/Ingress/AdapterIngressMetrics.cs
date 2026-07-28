namespace BunkFy.Modules.Ingestion.Application.Ingress;

using System.Diagnostics.Metrics;
using BunkFy.Modules.Ingestion.Application.Ports;

internal static class AdapterIngressMetrics
{
    internal const string MeterName = "bunkfy.ingestion.adapter_ingress";
    internal const string CounterName = "bunkfy.ingestion.adapter_ingress.decisions";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Decisions = Meter.CreateCounter<long>(
        CounterName,
        unit: "{decision}",
        description: "Third-party adapter ingress admission decisions.");

    public static void Record(AdapterIngressOperation operation, AdapterIngressGateOutcome outcome) =>
        Decisions.Add(
            1,
            new KeyValuePair<string, object?>("operation", OperationName(operation)),
            new KeyValuePair<string, object?>("outcome", OutcomeName(outcome)));

    private static string OperationName(AdapterIngressOperation operation) => operation switch
    {
        AdapterIngressOperation.Observation => "observation",
        AdapterIngressOperation.RemoteLeaseClaim => "remote-lease-claim",
        AdapterIngressOperation.RemoteLeaseRenew => "remote-lease-renew",
        AdapterIngressOperation.RemoteObservation => "remote-observation",
        AdapterIngressOperation.RemoteLeaseComplete => "remote-lease-complete",
        _ => "unknown"
    };

    private static string OutcomeName(AdapterIngressGateOutcome outcome) => outcome switch
    {
        AdapterIngressGateOutcome.Allowed => "allowed",
        AdapterIngressGateOutcome.QuotaRejected => "quota-rejected",
        AdapterIngressGateOutcome.PolicyRejected => "policy-rejected",
        AdapterIngressGateOutcome.ProviderUnavailable => "provider-unavailable",
        _ => "unknown"
    };
}
