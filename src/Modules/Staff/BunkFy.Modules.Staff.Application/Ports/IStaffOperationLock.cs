namespace BunkFy.Modules.Staff.Application.Ports;

public interface IStaffOperationLock
{
    Task<long?> GetStaffMemberRevisionAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken);

    Task<bool> TryAcquireStaffMemberAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken);
}
