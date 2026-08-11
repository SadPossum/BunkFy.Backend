namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffIdentityBootstrapper
{
    Task<StaffIdentityBootstrapResult> BootstrapAsync(
        StaffIdentityBootstrapRequest request,
        CancellationToken cancellationToken);
}

public sealed record StaffIdentityBootstrapRequest(
    Guid OperationId,
    string AuthSubjectId,
    string DisplayName,
    string? WorkEmail,
    string ActorId);

public sealed record StaffIdentityBootstrapResult(
    bool IsSuccess,
    string? ErrorCode);
