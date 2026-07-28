namespace BunkFy.Modules.Ingestion.Application.Ports;

public interface IAdapterIngressGate
{
    ValueTask<AdapterIngressGateDecision> AdmitAsync(
        AdapterIngressIdentity identity,
        AdapterIngressOperation operation,
        int permitCount,
        bool consumeQuota,
        CancellationToken cancellationToken);
}

public enum AdapterIngressOperation
{
    Unknown = 0,
    Observation = 1,
    RemoteLeaseClaim = 2,
    RemoteLeaseRenew = 3,
    RemoteObservation = 4,
    RemoteLeaseComplete = 5
}

public enum AdapterIngressGateOutcome
{
    Unknown = 0,
    Allowed = 1,
    QuotaRejected = 2,
    PolicyRejected = 3,
    ProviderUnavailable = 4
}

public enum AdapterIngressPolicyRejection
{
    None = 0,
    TenantSuspended = 1,
    GlobalStopped = 2,
    ScopeMismatch = 3
}

public sealed record AdapterIngressGateDecision(
    AdapterIngressGateOutcome Outcome,
    AdapterIngressPolicyRejection PolicyRejection,
    TimeSpan? RetryAfter)
{
    public bool IsAllowed => this.Outcome == AdapterIngressGateOutcome.Allowed;

    public static AdapterIngressGateDecision Allowed() =>
        new(AdapterIngressGateOutcome.Allowed, AdapterIngressPolicyRejection.None, null);

    public static AdapterIngressGateDecision QuotaRejected(TimeSpan retryAfter) =>
        new(AdapterIngressGateOutcome.QuotaRejected, AdapterIngressPolicyRejection.None, retryAfter);

    public static AdapterIngressGateDecision PolicyRejected(AdapterIngressPolicyRejection rejection) =>
        new(AdapterIngressGateOutcome.PolicyRejected, rejection, null);

    public static AdapterIngressGateDecision ProviderUnavailable() =>
        new(AdapterIngressGateOutcome.ProviderUnavailable, AdapterIngressPolicyRejection.None, null);
}
