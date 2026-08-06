namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using Gma.Framework.Pagination;
using BunkFy.Modules.Inventory.Contracts;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class ReservationRepository(
    ReservationsDbContext dbContext,
    IReservationProcessingRestrictionProjectionRepository restrictionProjections)
    : IReservationRepository
{
    public async Task AddAsync(
        Reservation reservation,
        CancellationToken cancellationToken)
    {
        dbContext.Reservations.Add(reservation);
        dbContext.OperationLocks.Add(new(
            Guid.NewGuid(),
            reservation.ScopeId,
            reservation.Id));
        await restrictionProjections.EnsureAsync(
            reservation.ScopeId,
            reservation.PropertyId,
            reservation.Id,
            reservation.CreatedAtUtc,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<Reservation?> GetAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        this.OrdinaryReservations()
            .Include(reservation => reservation.RequestedUnits)
            .Include(reservation => reservation.Guests)
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId && reservation.PropertyId == propertyId,
                cancellationToken);

    public Task<bool> ExistsAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        this.OrdinaryReservations()
            .AsNoTracking()
            .AnyAsync(
                reservation => reservation.Id == reservationId && reservation.PropertyId == propertyId,
                cancellationToken);

    public Task<Reservation?> GetForDataRightsAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.Reservations
            .Include(reservation => reservation.RequestedUnits)
            .Include(reservation => reservation.Guests)
            .FirstOrDefaultAsync(
                reservation =>
                    reservation.Id == reservationId &&
                    reservation.PropertyId == propertyId,
                cancellationToken);

    public Task<Reservation?> GetForRequiredContinuationAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.Reservations
            .Include(reservation => reservation.RequestedUnits)
            .Include(reservation => reservation.Guests)
            .FirstOrDefaultAsync(
                reservation =>
                    reservation.Id == reservationId &&
                    reservation.PropertyId == propertyId,
                cancellationToken);

    public Task<Reservation?> GetAsyncByReservationId(
        Guid reservationId,
        CancellationToken cancellationToken) =>
        this.OrdinaryReservations()
            .Include(reservation => reservation.RequestedUnits)
            .Include(reservation => reservation.Guests)
            .FirstOrDefaultAsync(
                reservation => reservation.Id == reservationId,
                cancellationToken);

    public Task<Reservation?> GetForRequiredContinuationByReservationIdAsync(
        Guid reservationId,
        CancellationToken cancellationToken) =>
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
                reservation => !reservation.IsAnonymised &&
                               reservation.SourceSystem == sourceSystem &&
                               reservation.SourceReference == sourceReference,
                cancellationToken);

    public Task<bool> ExternalSourceExistsAsync(
        string sourceSystem,
        string sourceReference,
        CancellationToken cancellationToken) =>
        dbContext.Reservations.AsNoTracking().AnyAsync(
            reservation =>
                !reservation.IsAnonymised &&
                reservation.SourceSystem == sourceSystem &&
                reservation.SourceReference == sourceReference,
            cancellationToken);

    public async Task<ReservationListResponse> ListAsync(
        Guid propertyId,
        IReadOnlyCollection<ReservationStatus>? statuses,
        string? search,
        ReservationListOrder order,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<Reservation> query = this.OrdinaryReservations()
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
        ReservationListRow[] rows = await ordered
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize + 1)
            .Select(reservation => new ReservationListRow(
                reservation.Id,
                reservation.PropertyId,
                reservation.Arrival,
                reservation.Departure,
                reservation.ExpectedArrivalTime,
                reservation.ExpectedDepartureTime,
                reservation.PrimaryGuestName,
                reservation.GuestCount,
                reservation.RequestedUnits.Count,
                reservation.Source,
                reservation.Status))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        bool hasMore = rows.Length > pageRequest.PageSize;
        return new(
            rows.Take(pageRequest.PageSize).Select(Map).ToArray(),
            pageRequest.Page,
            pageRequest.PageSize,
            hasMore);
    }

    private IQueryable<Reservation> OrdinaryReservations() =>
        dbContext.Reservations.Where(reservation =>
            !reservation.IsAnonymised &&
            dbContext.ProcessingRestrictionProjections.Any(projection =>
                projection.PropertyId == reservation.PropertyId &&
                projection.ReservationId == reservation.Id &&
                projection.ContractVersion ==
                    ReservationProcessingRestrictionContract.CurrentVersion &&
                !projection.IsRestricted));

    private static ReservationListItemDto Map(ReservationListRow reservation) => new(
        reservation.ReservationId,
        reservation.PropertyId,
        reservation.Arrival,
        reservation.Departure,
        reservation.ExpectedArrivalTime,
        reservation.ExpectedDepartureTime,
        reservation.PrimaryGuestName,
        reservation.GuestCount,
        reservation.InventoryUnitCount,
        reservation.Source == ReservationSource.Direct
            ? ReservationSourceKind.Direct
            : ReservationSourceKind.External,
        MapStatus(reservation.Status));

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

    private sealed record ReservationListRow(
        Guid ReservationId,
        Guid PropertyId,
        DateOnly Arrival,
        DateOnly Departure,
        TimeOnly? ExpectedArrivalTime,
        TimeOnly? ExpectedDepartureTime,
        string PrimaryGuestName,
        int GuestCount,
        int InventoryUnitCount,
        ReservationSource Source,
        ReservationState Status);
}
