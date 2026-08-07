namespace BunkFy.Modules.Guests.Application.Handlers;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Domain.Aggregates;
using Gma.Framework.Scoping;

internal sealed class GuestMutationCoordinator(
    IGuestProfileRepository profiles,
    IGuestOperationLock operationLock,
    IScopeContext scopeContext)
{
    public async Task<GuestProfile?> AcquireCreationAsync(
        Guid operationId,
        CancellationToken cancellationToken)
    {
        string tenantId = scopeContext.IsEnabled
            ? scopeContext.ScopeId?.Trim() ?? string.Empty
            : string.Empty;
        if (tenantId.Length == 0 || operationId == Guid.Empty)
        {
            return null;
        }

        await operationLock.AcquireGuestAsync(
            tenantId,
            operationId,
            cancellationToken).ConfigureAwait(false);
        GuestProfile? existing = await profiles.GetByIdAsync(
            operationId,
            cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        if (existing.Id != operationId ||
            !string.Equals(existing.ScopeId, tenantId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Guest creation lookup returned a profile outside its operation scope.");
        }

        return existing;
    }

    public async Task<GuestProfile?> AcquireVisibleAsync(
        Guid propertyId,
        Guid guestId,
        CancellationToken cancellationToken)
    {
        GuestProfile? profile = await profiles.GetVisibleAsync(
            propertyId,
            guestId,
            cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        await operationLock.AcquireGuestAsync(
            profile.ScopeId,
            guestId,
            cancellationToken).ConfigureAwait(false);
        profile = await profiles.GetVisibleAsync(
            propertyId,
            guestId,
            cancellationToken).ConfigureAwait(false);
        if (profile is null)
        {
            return null;
        }

        if (profile.Id != guestId)
        {
            throw new InvalidOperationException(
                "The Guest mutation lookup returned a profile outside its requested property scope.");
        }

        return profile;
    }
}
