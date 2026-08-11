namespace BunkFy.Modules.Staff.Application.Ports;

public interface IStaffOnboardingProvisioningOperationRepository
{
    Task<StaffMemberMutationOperationRecord?> GetAsync(
        Guid operationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        StaffMemberMutationOperationRecord operation,
        CancellationToken cancellationToken);
}
