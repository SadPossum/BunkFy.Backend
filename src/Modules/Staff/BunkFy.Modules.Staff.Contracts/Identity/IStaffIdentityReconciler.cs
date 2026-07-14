namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffIdentityReconciler
{
    Task<StaffIdentityReconciliationResult> ReconcileAsync(
        StaffIdentityReconciliationRequest request,
        CancellationToken cancellationToken);
}

public sealed record StaffIdentityReconciliationRequest(
    string AuthSubjectId,
    string DisplayName,
    string? WorkEmail,
    bool IsActive,
    string ActorId,
    string Reason);

public sealed record StaffIdentityReconciliationResult(
    bool IsSuccess,
    string? ErrorCode);
