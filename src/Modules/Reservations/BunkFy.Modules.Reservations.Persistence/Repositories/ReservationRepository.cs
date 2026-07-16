namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using Gma.Framework.Pagination;
using BunkFy.Modules.Inventory.Contracts;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class ReservationRepository(ReservationsDbContext dbContext) : IReservationRepository
{
    public Task AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        dbContext.Reservations.Add(reservation);
        return Task.CompletedTask;
    }

    public Task<Reservation?> GetAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.Reservations
            .Include(reservation => reservation.RequestedUnits)
            .Include(reservation => reservation.Guests)
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId && reservation.PropertyId == propertyId,
                cancellationToken);

    public Task<Reservation?> GetAsyncByReservationId(Guid reservationId, CancellationToken cancellationToken) =>
        dbContext.Reservations
            .Include(reservation => reservation.RequestedUnits)
            .Include(reservation => reservation.Guests)
            .FirstOrDefaultAsync(reservation => reservation.Id == reservationId, cancellationToken);

    public Task<Reservation?> GetByExternalSourceAsync(
        string sourceSystem,
        string sourceReference,
        CancellationToken cancellationToken) =>
        dbContext.Reservations
            .Include(reservation => reservation.RequestedUnits)
            .Include(reservation => reservation.Guests)
            .FirstOrDefaultAsync(
                reservation => reservation.SourceSystem == sourceSystem &&
                               reservation.SourceReference == sourceReference,
                cancellationToken);

    public Task<bool> ExternalSourceExistsAsync(
        string sourceSystem,
        string sourceReference,
        CancellationToken cancellationToken) =>
        dbContext.Reservations.AsNoTracking().AnyAsync(
            reservation => reservation.SourceSystem == sourceSystem && reservation.SourceReference == sourceReference,
            cancellationToken);

    public async Task<ReservationListResponse> ListAsync(
        Guid propertyId,
        IReadOnlyCollection<ReservationStatus>? statuses,
        string? search,
        ReservationListOrder order,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<Reservation> query = dbContext.Reservations
            .AsNoTracking()
            .Where(reservation => reservation.PropertyId == propertyId);

        if (statuses is { Count: > 0 })
        {
            ReservationState[] states = statuses
                .Select(MapStatus)
                .Where(state => state.HasValue)
                .Select(state => state!.Value)
                .Distinct()
                .ToArray();
            query = query.Where(reservation => states.Contains(reservation.Status));
        }

        string? normalizedSearch = string.IsNullOrWhiteSpace(search)
            ? null
            : search.Trim().ToUpperInvariant();
        if (normalizedSearch is not null)
        {
#pragma warning disable CA1304, CA1311, CA1862 // StringComparison overloads are not translated by the supported EF Core providers.
            query = query.Where(reservation =>
                reservation.PrimaryGuestName.ToUpper().Contains(normalizedSearch) ||
                (reservation.Email != null && reservation.Email.ToUpper().Contains(normalizedSearch)) ||
                (reservation.Phone != null && reservation.Phone.ToUpper().Contains(normalizedSearch)) ||
                (reservation.SourceSystem != null && reservation.SourceSystem.ToUpper().Contains(normalizedSearch)) ||
                (reservation.SourceReference != null && reservation.SourceReference.ToUpper().Contains(normalizedSearch)));
#pragma warning restore CA1304, CA1311, CA1862
        }

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        IOrderedQueryable<Reservation> ordered = order switch
        {
            ReservationListOrder.ArrivalAscending => query
                .OrderBy(reservation => reservation.Arrival)
                .ThenBy(reservation => reservation.ExpectedArrivalTime)
                .ThenBy(reservation => reservation.Id),
            ReservationListOrder.DepartureDescending => query
                .OrderByDescending(reservation => reservation.Departure)
                .ThenByDescending(reservation => reservation.ExpectedDepartureTime)
                .ThenBy(reservation => reservation.Id),
            _ => query
                .OrderByDescending(reservation => reservation.CreatedAtUtc)
                .ThenBy(reservation => reservation.Id)
        };
        Reservation[] rows = await ordered
            .AsSplitQuery()
            .Include(reservation => reservation.RequestedUnits)
            .Include(reservation => reservation.Guests)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new(rows.Select(Map).ToArray(), pageRequest.Page, pageRequest.PageSize, totalCount);
    }

    private static ReservationDto Map(Reservation reservation) => new(
        reservation.Id,
        reservation.PropertyId,
        reservation.Arrival,
        reservation.Departure,
        reservation.ExpectedArrivalTime,
        reservation.ExpectedDepartureTime,
        reservation.RequestedUnits.Select(unit => unit.InventoryUnitId).ToArray(),
        reservation.PrimaryGuestName,
        reservation.Email,
        reservation.Phone,
        reservation.GuestCount,
        reservation.Source == ReservationSource.Direct ? ReservationSourceKind.Direct : ReservationSourceKind.External,
        reservation.SourceSystem,
        reservation.SourceReference,
        reservation.Notes,
        MapStatus(reservation.Status),
        reservation.AllocationRequestId,
        reservation.AllocationId,
        reservation.AllocationVersion,
        reservation.AllocationRejection.HasValue
            ? (InventoryAllocationRejectionReason)(int)reservation.AllocationRejection.Value
            : null,
        reservation.PendingAllocationAmendmentId,
        reservation.LastAllocationAmendmentRejectionCode.HasValue
            ? (InventoryAllocationRejectionReason)reservation.LastAllocationAmendmentRejectionCode.Value
            : null,
        reservation.DetailsRevision,
        (ReservationDetailsChangeOriginKind)(int)reservation.LastDetailsChangeOrigin,
        reservation.Version,
        reservation.CreatedAtUtc,
        reservation.UpdatedAtUtc,
        reservation.PendingStayBusinessDate,
        reservation.PendingStayActorId,
        reservation.CheckedInBusinessDate,
        reservation.CheckedInAtUtc,
        reservation.CheckedInBy,
        reservation.NoShowBusinessDate,
        reservation.NoShowAtUtc,
        reservation.NoShowBy,
        reservation.CheckedOutBusinessDate,
        reservation.CheckedOutAtUtc,
        reservation.CheckedOutBy,
        reservation.Guests.Where(guest => guest.IsCurrent).Select(guest => new ReservationGuestDto(
            guest.GuestId,
            (ReservationGuestRoleKind)(int)guest.Role)).ToArray());

    private static ReservationStatus MapStatus(ReservationState status) => status switch
    {
        ReservationState.PendingAllocation => ReservationStatus.PendingAllocation,
        ReservationState.Confirmed => ReservationStatus.Confirmed,
        ReservationState.AllocationRejected => ReservationStatus.AllocationRejected,
        ReservationState.CancellationPending => ReservationStatus.CancellationPending,
        ReservationState.Cancelled => ReservationStatus.Cancelled,
        ReservationState.CheckedIn => ReservationStatus.CheckedIn,
        ReservationState.NoShowPending => ReservationStatus.NoShowPending,
        ReservationState.NoShow => ReservationStatus.NoShow,
        ReservationState.CheckoutPending => ReservationStatus.CheckoutPending,
        ReservationState.CheckedOut => ReservationStatus.CheckedOut,
        _ => ReservationStatus.Unknown
    };

    private static ReservationState? MapStatus(ReservationStatus status) => status switch
    {
        ReservationStatus.PendingAllocation => ReservationState.PendingAllocation,
        ReservationStatus.Confirmed => ReservationState.Confirmed,
        ReservationStatus.AllocationRejected => ReservationState.AllocationRejected,
        ReservationStatus.CancellationPending => ReservationState.CancellationPending,
        ReservationStatus.Cancelled => ReservationState.Cancelled,
        ReservationStatus.CheckedIn => ReservationState.CheckedIn,
        ReservationStatus.NoShowPending => ReservationState.NoShowPending,
        ReservationStatus.NoShow => ReservationState.NoShow,
        ReservationStatus.CheckoutPending => ReservationState.CheckoutPending,
        ReservationStatus.CheckedOut => ReservationState.CheckedOut,
        _ => null
    };
}
