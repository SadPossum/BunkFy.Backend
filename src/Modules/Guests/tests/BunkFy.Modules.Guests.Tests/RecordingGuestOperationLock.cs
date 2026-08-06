namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Application.Ports;

internal sealed class RecordingGuestOperationLock(Action? onGuestAcquired = null)
    : IGuestOperationLock
{
    public List<(string TenantId, Guid GuestId)> GuestAcquisitions { get; } = [];

    public Task AcquireGuestAsync(
        string tenantId,
        Guid guestId,
        CancellationToken cancellationToken)
    {
        this.GuestAcquisitions.Add((tenantId, guestId));
        onGuestAcquired?.Invoke();
        return Task.CompletedTask;
    }

    public Task AcquirePropertiesAsync(
        string tenantId,
        IReadOnlyCollection<Guid> propertyIds,
        CancellationToken cancellationToken) => Task.CompletedTask;
}
