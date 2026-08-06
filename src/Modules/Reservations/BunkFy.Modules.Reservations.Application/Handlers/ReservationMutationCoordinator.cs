namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Scoping;

internal sealed class ReservationMutationCoordinator(
    IReservationRepository reservations,
    IReservationOperationLock operationLock,
    IScopeContext scopeContext)
{
    public Task<Reservation?> AcquireOperationalAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) => this.AcquireAndReloadAsync(
        reservationId,
        token => reservations.GetAsync(propertyId, reservationId, token),
        cancellationToken);

    public Task<Reservation?> AcquireDataRightsAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) => this.AcquireAndReloadAsync(
        reservationId,
        token => reservations.GetForDataRightsAsync(
            propertyId,
            reservationId,
            token),
        cancellationToken);

    public Task<Reservation?> AcquireRequiredContinuationAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) => this.AcquireAndReloadAsync(
        reservationId,
        token => reservations.GetForRequiredContinuationAsync(
            propertyId,
            reservationId,
            token),
        cancellationToken);

    public Task<Reservation?> AcquireRequiredContinuationByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken) => this.AcquireAndReloadAsync(
        reservationId,
        token => reservations.GetForRequiredContinuationByReservationIdAsync(
            reservationId,
            token),
        cancellationToken);

    public async Task<bool> AcquireExistingAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        if (!this.TryGetTenantId(reservationId, out string tenantId))
        {
            return false;
        }

        return await operationLock.TryAcquireExistingAsync(
            tenantId,
            reservationId,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<Reservation?> AcquireAndReloadAsync(
        Guid reservationId,
        Func<CancellationToken, Task<Reservation?>> reload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reload);
        if (!this.TryGetTenantId(reservationId, out string tenantId) ||
            !await operationLock.TryAcquireExistingAsync(
                tenantId,
                reservationId,
                cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        Reservation? reloaded = await reload(cancellationToken)
            .ConfigureAwait(false);
        return reloaded is not null &&
            reloaded.Id == reservationId &&
            string.Equals(reloaded.ScopeId, tenantId, StringComparison.Ordinal)
                ? reloaded
                : null;
    }

    private bool TryGetTenantId(
        Guid reservationId,
        out string tenantId)
    {
        tenantId = scopeContext.IsEnabled
            ? scopeContext.ScopeId?.Trim() ?? string.Empty
            : string.Empty;
        return reservationId != Guid.Empty &&
            tenantId.Length > 0;
    }
}
