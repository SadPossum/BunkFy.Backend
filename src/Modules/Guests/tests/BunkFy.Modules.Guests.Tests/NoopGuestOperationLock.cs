namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Application.Ports;

internal sealed class NoopGuestOperationLock : IGuestOperationLock
{
    public Task AcquireGuestAsync(
        string tenantId,
        Guid guestId,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public Task AcquirePropertiesAsync(
        string tenantId,
        IReadOnlyCollection<Guid> propertyIds,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
