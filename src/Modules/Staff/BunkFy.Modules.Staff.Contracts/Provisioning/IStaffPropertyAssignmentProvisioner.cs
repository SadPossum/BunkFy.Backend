namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffPropertyAssignmentProvisioner
{
    Task<StaffPropertyAssignmentProvisioningResult> ReconcileAsync(
        StaffPropertyAssignmentProvisioningRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record StaffPropertyAssignmentProvisioningRequest(
    Guid StaffMemberId,
    IReadOnlyCollection<Guid> PropertyIds,
    string ActorId,
    string Reason);

public sealed record StaffPropertyAssignmentProvisioningResult(
    bool IsSuccess,
    IReadOnlyCollection<Guid> PropertyIds,
    string? ErrorCode);
