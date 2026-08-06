namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.DataGovernance;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Application.Validation;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CreateReservationCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Equivalent_retry_replays_current_reservation_without_revalidating_inventory()
    {
        Guid firstUnitId = Guid.NewGuid();
        Guid secondUnitId = Guid.NewGuid();
        Guid operationId = Guid.NewGuid();
        RecordingReservationRepository reservations = new();
        RecordingInventoryProjection inventory = new();
        CreateReservationCommandHandler handler = CreateHandler(
            reservations,
            inventory);
        CreateReservationCommand first = CreateCommand(
            operationId,
            [firstUnitId, secondUnitId],
            "  Ada Guest  ",
            "  ada@example.test  ");
        CreateReservationCommand retry = CreateCommand(
            operationId,
            [secondUnitId, firstUnitId],
            "Ada Guest",
            "ada@example.test");

        Result<ReservationMutationReceiptDto> created = await handler.HandleAsync(
            first,
            CancellationToken.None);
        Result<ReservationMutationReceiptDto> replayed = await handler.HandleAsync(
            retry,
            CancellationToken.None);

        Assert.True(created.IsSuccess);
        Assert.True(replayed.IsSuccess);
        Assert.Equal(operationId, created.Value.ReservationId);
        Assert.Equal(created.Value, replayed.Value);
        Assert.Equal(1, reservations.AddCount);
        Assert.Equal(1, inventory.ValidationCount);
    }

    [Fact]
    public async Task Retry_returns_current_receipt_after_asynchronous_allocation_confirmation()
    {
        Guid operationId = Guid.NewGuid();
        RecordingReservationRepository reservations = new();
        CreateReservationCommandHandler handler = CreateHandler(
            reservations,
            new RecordingInventoryProjection());
        CreateReservationCommand command = CreateCommand(
            operationId,
            [Guid.NewGuid()],
            "Ada Guest",
            null);
        Assert.True((await handler.HandleAsync(
            command,
            CancellationToken.None)).IsSuccess);
        Reservation reservation = Assert.IsType<Reservation>(reservations.Current);
        Assert.True(reservation.ConfirmAllocation(
            reservation.AllocationRequestId,
            Guid.NewGuid(),
            allocationVersion: 1,
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);

        Result<ReservationMutationReceiptDto> replayed = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(replayed.IsSuccess);
        Assert.Equal(ReservationStatus.Confirmed, replayed.Value.Status);
        Assert.Equal(reservation.Version, replayed.Value.Version);
        Assert.Equal(1, reservations.AddCount);
    }

    [Fact]
    public async Task Reusing_operation_id_for_changed_request_returns_conflict()
    {
        Guid operationId = Guid.NewGuid();
        RecordingReservationRepository reservations = new();
        RecordingInventoryProjection inventory = new();
        CreateReservationCommandHandler handler = CreateHandler(
            reservations,
            inventory);
        Assert.True((await handler.HandleAsync(
            CreateCommand(operationId, [Guid.NewGuid()], "Ada Guest", null),
            CancellationToken.None)).IsSuccess);

        Result<ReservationMutationReceiptDto> conflict = await handler.HandleAsync(
            CreateCommand(
                operationId,
                reservations.Current!.RequestedUnits.Select(unit => unit.InventoryUnitId).ToArray(),
                "Different Guest",
                null),
            CancellationToken.None);

        Assert.True(conflict.IsFailure);
        Assert.Equal(
            ReservationsApplicationErrors.CreationOperationConflict,
            conflict.Error);
        Assert.Equal(1, reservations.AddCount);
        Assert.Equal(1, inventory.ValidationCount);
    }

    [Fact]
    public void Validator_requires_operation_id()
    {
        CreateReservationCommand command = CreateCommand(
            Guid.Empty,
            [Guid.NewGuid()],
            "Ada Guest",
            null);

        string[] errors = new CreateReservationCommandValidator()
            .Validate(command)
            .ToArray();

        Assert.Contains("OperationId is required.", errors);
    }

    private static CreateReservationCommandHandler CreateHandler(
        RecordingReservationRepository reservations,
        RecordingInventoryProjection inventory) => new(
        reservations,
        ReservationMutationTestSupport.Create(reservations),
        inventory,
        new AllowingCountryPolicy(),
        new TestScopeContext(),
        new TestClock(),
        new TestIdGenerator());

    private static CreateReservationCommand CreateCommand(
        Guid operationId,
        IReadOnlyCollection<Guid> unitIds,
        string guestName,
        string? email) => new(
        operationId,
        Guid.Parse("10000000-0000-0000-0000-000000000001"),
        new DateOnly(2026, 8, 10),
        new DateOnly(2026, 8, 12),
        unitIds,
        guestName,
        email,
        "  +44 20 1234 5678  ",
        1,
        ReservationSourceKind.Direct,
        "  ",
        null,
        "  Quiet room  ",
        new TimeOnly(15, 0),
        new TimeOnly(11, 0),
        "user:operator");

    private sealed class RecordingReservationRepository : IReservationRepository
    {
        public Reservation? Current { get; private set; }
        public int AddCount { get; private set; }

        public Task AddAsync(
            Reservation reservation,
            CancellationToken cancellationToken)
        {
            this.Current = reservation;
            this.AddCount++;
            return Task.CompletedTask;
        }

        public Task<Reservation?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Current is not null &&
                this.Current.PropertyId == propertyId &&
                this.Current.Id == reservationId
                    ? this.Current
                    : null);

        public Task<Reservation?> GetForDataRightsAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            this.GetAsync(propertyId, reservationId, cancellationToken);

        public Task<Reservation?> GetAsyncByReservationId(
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Current?.Id == reservationId ? this.Current : null);

        public Task<Reservation?> GetByExternalSourceAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) =>
            Task.FromResult<Reservation?>(null);

        public Task<bool> ExternalSourceExistsAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<ReservationListResponse> ListAsync(
            Guid propertyId,
            IReadOnlyCollection<ReservationStatus>? statuses,
            string? search,
            ReservationListOrder order,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingInventoryProjection : IInventoryProjectionRepository
    {
        public int ValidationCount { get; private set; }

        public Task<InventoryUnitSelectionValidation> ValidateSelectionAsync(
            Guid propertyId,
            IReadOnlyCollection<Guid> inventoryUnitIds,
            CancellationToken cancellationToken)
        {
            this.ValidationCount++;
            return Task.FromResult(InventoryUnitSelectionValidation.Valid);
        }

        public Task ApplyUnitAsync(
            ReservationInventoryUnitWriteModel unit,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ApplyBlockAsync(
            ReservationInventoryBlockWriteModel block,
            CancellationToken cancellationToken) => throw new NotSupportedException();

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

    private sealed class AllowingCountryPolicy : IReservationCountryPolicyAdmission
    {
        public Task<CountryPolicyDecision> EvaluateAsync(
            Guid propertyId,
            string purposeCode,
            CountryPolicySurface surface,
            string sourceProvenance,
            CancellationToken cancellationToken) =>
            Task.FromResult(CountryPolicyDecision.Allow(new CountryPolicyEvidence(
                "GB",
                "gb-hostel",
                1,
                "eu-west-2",
                "uk-no-transfer",
                "reservation-operational",
                1,
                new string('a', 64),
                purposeCode,
                surface,
                sourceProvenance,
                CountryPolicyApprovalState.Approved,
                Now.AddDays(-1),
                Now.AddDays(30),
                Now,
                [])));
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
