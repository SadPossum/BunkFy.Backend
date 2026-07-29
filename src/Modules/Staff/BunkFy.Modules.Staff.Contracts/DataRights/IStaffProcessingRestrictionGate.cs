namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffProcessingRestrictionGate
{
    Task<StaffProcessingRestrictionGateResult> EvaluateAsync(
        StaffProcessingRestrictionGateRequest request,
        CancellationToken cancellationToken);
}

public sealed record StaffProcessingRestrictionGateRequest(
    string TenantId,
    Guid StaffMemberId,
    int ContractVersion = StaffProcessingRestrictionContract.CurrentVersion);

public sealed record StaffProcessingRestrictionGateResult(
    StaffProcessingRestrictionDecision Decision,
    int? ObservedContractVersion,
    long? ProjectionRevision)
{
    public bool IsAllowed =>
        this.Decision == StaffProcessingRestrictionDecision.Allowed;

    public static StaffProcessingRestrictionGateResult Allowed(
        int contractVersion,
        long projectionRevision) => new(
        StaffProcessingRestrictionDecision.Allowed,
        contractVersion,
        projectionRevision);

    public static StaffProcessingRestrictionGateResult Restricted(
        int contractVersion,
        long projectionRevision) => new(
        StaffProcessingRestrictionDecision.Restricted,
        contractVersion,
        projectionRevision);

    public static StaffProcessingRestrictionGateResult Unknown { get; } = new(
        StaffProcessingRestrictionDecision.Unknown,
        null,
        null);

    public static StaffProcessingRestrictionGateResult Unsupported(
        int? observedContractVersion,
        long? projectionRevision = null) => new(
        StaffProcessingRestrictionDecision.UnsupportedContractVersion,
        observedContractVersion,
        projectionRevision);
}

public enum StaffProcessingRestrictionDecision
{
    Unknown = 0,
    Allowed = 1,
    Restricted = 2,
    UnsupportedContractVersion = 3
}
