namespace BunkFy.Modules.Guests.Contracts;

public interface IGuestProcessingRestrictionGate
{
    Task<GuestProcessingRestrictionGateResult> EvaluateAsync(
        GuestProcessingRestrictionGateRequest request,
        CancellationToken cancellationToken);
}

public sealed record GuestProcessingRestrictionGateRequest(
    string TenantId,
    Guid PropertyId,
    Guid GuestId,
    int ContractVersion = GuestProcessingRestrictionContract.CurrentVersion);

public sealed record GuestProcessingRestrictionGateResult(
    GuestProcessingRestrictionDecision Decision,
    int? ObservedContractVersion,
    long? ProjectionRevision)
{
    public bool IsAllowed => this.Decision == GuestProcessingRestrictionDecision.Allowed;

    public static GuestProcessingRestrictionGateResult Allowed(
        int contractVersion,
        long projectionRevision) => new(
        GuestProcessingRestrictionDecision.Allowed,
        contractVersion,
        projectionRevision);

    public static GuestProcessingRestrictionGateResult Restricted(
        int contractVersion,
        long projectionRevision) => new(
        GuestProcessingRestrictionDecision.Restricted,
        contractVersion,
        projectionRevision);

    public static GuestProcessingRestrictionGateResult Unknown { get; } = new(
        GuestProcessingRestrictionDecision.Unknown,
        null,
        null);

    public static GuestProcessingRestrictionGateResult Unsupported(
        int? observedContractVersion,
        long? projectionRevision = null) => new(
        GuestProcessingRestrictionDecision.UnsupportedContractVersion,
        observedContractVersion,
        projectionRevision);
}

public enum GuestProcessingRestrictionDecision
{
    Unknown = 0,
    Allowed = 1,
    Restricted = 2,
    UnsupportedContractVersion = 3
}
