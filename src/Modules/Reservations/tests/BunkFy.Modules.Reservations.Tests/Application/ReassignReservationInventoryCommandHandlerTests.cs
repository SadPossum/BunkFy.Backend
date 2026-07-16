namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReassignReservationInventoryCommandHandlerTests
{
    [Fact]
    public async Task Staff_reassignment_stays_pending_until_inventory_confirms_it()
    {
        Guid currentUnitId = Guid.NewGuid();
        Guid targetUnitId = Guid.NewGuid();
        Reservation reservation = CreateConfirmedReservation(currentUnitId);
        reservation.ClearDomainEvents();
        ReassignReservationInventoryCommandHandler handler = new(
            new FakeReservationRepository(reservation),
            new FakeInventoryProjectionRepository(),
            new TestClock(),
            new TestIdGenerator());
        Guid amendmentRequestId = Guid.NewGuid();

        Result<ReservationDto> result = await handler.HandleAsync(
            new(
                reservation.PropertyId,
                reservation.Id,
                amendmentRequestId,
                [targetUnitId],
                reservation.DetailsRevision,
                "user:operator-a"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(amendmentRequestId, result.Value.PendingAllocationAmendmentId);
        Assert.Equal([currentUnitId], result.Value.InventoryUnitIds);
        ReservationAllocationAmendmentRequestedDomainEvent domainEvent =
            Assert.IsType<ReservationAllocationAmendmentRequestedDomainEvent>(Assert.Single(reservation.DomainEvents));
        Assert.Equal([targetUnitId], domainEvent.InventoryUnitIds);
        Assert.Equal(ReservationDetailsChangeOrigin.Staff, reservation.PendingDetailsChangeOrigin);
    }

    private static Reservation CreateConfirmedReservation(Guid inventoryUnitId)
    {
        Reservation reservation = Reservation.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [inventoryUnitId],
            "Ada Guest",
            "ada@example.test",
            null,
            1,
            ReservationSource.Direct,
            sourceSystem: null,
            sourceReference: null,
            notes: null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationDetailsChangeOrigin.Staff,
            initialDetailsActorId: null,
            initialAdapterConnectionId: null,
            initialExternalOperationId: null,
            Guid.NewGuid(),
            Now).Value;
        Assert.True(reservation.ConfirmAllocation(
            reservation.AllocationRequestId,
            Guid.NewGuid(),
            allocationVersion: 1,
            Guid.NewGuid(),
            Now).IsSuccess);
        return reservation;
    }

    private sealed class FakeReservationRepository(Reservation reservation) : IReservationRepository
    {
        public Task AddAsync(Reservation value, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Reservation?> GetAsync(Guid propertyId, Guid reservationId, CancellationToken cancellationToken) =>
            Task.FromResult<Reservation?>(
                reservation.PropertyId == propertyId && reservation.Id == reservationId ? reservation : null);

        public Task<Reservation?> GetAsyncByReservationId(Guid reservationId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Reservation?> GetByExternalSourceAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> ExternalSourceExistsAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<ReservationListResponse> ListAsync(
            Guid propertyId,
            IReadOnlyCollection<ReservationStatus>? statuses,
            string? search,
            ReservationListOrder order,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeInventoryProjectionRepository : IInventoryProjectionRepository
    {
        public Task<InventoryUnitSelectionValidation> ValidateSelectionAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken) => Task.FromResult(InventoryUnitSelectionValidation.Valid);

        public Task ApplyUnitAsync(ReservationInventoryUnitWriteModel unit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ApplyBlockAsync(ReservationInventoryBlockWriteModel block, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task ReleaseBlockAsync(
            string scopeId,
            Guid propertyId,
            Guid inventoryUnitId,
            Guid blockId,
            long version,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ApplyAllocationAsync(
            ReservationInventoryAllocationWriteModel allocation,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ReleaseAllocationAsync(
            string scopeId,
            Guid allocationId,
            Guid reservationId,
            long version,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }

    private static readonly DateTimeOffset Now = new(2026, 7, 14, 12, 0, 0, TimeSpan.Zero);
}
