namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Application.Ports;

internal sealed class NoopStaffCreationOperationLock : IStaffCreationOperationLock
{
    public Task AcquireAsync(
        string tenantId,
        Guid operationId,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
