namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Application.Ports;

internal sealed class NoopStaffOperationLock : IStaffOperationLock
{
    public Task<long?> GetStaffMemberRevisionAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        Task.FromResult<long?>(1);

    public Task<bool> TryAcquireStaffMemberAsync(
        string tenantId,
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        Task.FromResult(true);
}
