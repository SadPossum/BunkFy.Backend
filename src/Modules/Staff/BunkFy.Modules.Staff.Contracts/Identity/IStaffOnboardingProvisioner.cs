namespace BunkFy.Modules.Staff.Contracts;

public interface IStaffOnboardingProvisioner
{
    Task<StaffOnboardingProvisioningResult> ProvisionAsync(
        StaffOnboardingProvisioningRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record StaffOnboardingProvisioningRequest(
    string AuthSubjectId,
    string DisplayName,
    string? LegalName,
    string? WorkEmail,
    string? WorkPhone,
    string? EmployeeNumber,
    string? JobTitle,
    string? Department,
    string ActorId,
    string Reason);

public sealed record StaffOnboardingProvisioningResult(
    bool IsSuccess,
    Guid? StaffMemberId,
    string? ErrorCode);
