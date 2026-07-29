namespace BunkFy.Modules.Staff.Application.Ports;

public interface IStaffOperationLock
{
    Task<bool> TryAcquireStaffMemberAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken);
}
