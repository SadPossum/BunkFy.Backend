namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ApplyReservationAnonymisationCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 1, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task Approved_operation_redacts_once_and_replays_exactly()
    {
        Reservation reservation = CreateTerminalReservation();
        RecordingAnonymisationRepository anonymisation = new();
        RecordingOperationLock operationLock = new();
        RecordingEligibility eligibility = new();
        RecordingApprovalGate approval = new(CreateApprovalEvidence());
        ApplyReservationAnonymisationCommandHandler handler = new(
            new StubReservationRepository(reservation),
            anonymisation,
            operationLock,
            eligibility,
            approval,
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        ApplyReservationAnonymisationCommand command =
            CreateCommand(reservation);

        Result<ReservationAnonymisationReceiptDto> first =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<ReservationAnonymisationReceiptDto> replay =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.True(reservation.IsAnonymised);
        Assert.Equal(command.ExpectedReservationVersion + 1, first.Value.ResultingReservationVersion);
        Assert.Equal(command.ExpectedDetailsRevision + 1, first.Value.ResultingDetailsRevision);
        Assert.Equal(2, first.Value.RedactedHistoryCount);
        Assert.Equal(1, first.Value.ReducedExternalOperationCount);
        Assert.Equal(1, first.Value.SuppressedReminderCount);
        Assert.Single(operationLock.ReservationIds);
        Assert.Equal(1, approval.CallCount);
        Assert.Equal(1, eligibility.CallCount);
        Assert.Equal(1, anonymisation.AddCount);
    }

    [Fact]
    public async Task Changed_retry_and_blocked_eligibility_fail_closed()
    {
        Reservation reservation = CreateTerminalReservation();
        RecordingAnonymisationRepository anonymisation = new();
        ApplyReservationAnonymisationCommand command =
            CreateCommand(reservation);
        ApplyReservationAnonymisationCommandHandler handler = new(
            new StubReservationRepository(reservation),
            anonymisation,
            new RecordingOperationLock(),
            new RecordingEligibility(),
            new RecordingApprovalGate(CreateApprovalEvidence()),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        Assert.True((await handler.HandleAsync(
            command,
            CancellationToken.None)).IsSuccess);

        Result<ReservationAnonymisationReceiptDto> changed =
            await handler.HandleAsync(
                command with { OperationRevision = command.OperationRevision + 1 },
                CancellationToken.None);
        Assert.Equal(
            ReservationsApplicationErrors.AnonymisationIdempotencyConflict,
            changed.Error);

        Reservation blockedReservation = CreateTerminalReservation();
        ApplyReservationAnonymisationCommandHandler blockedHandler = new(
            new StubReservationRepository(blockedReservation),
            new RecordingAnonymisationRepository(),
            new RecordingOperationLock(),
            new RecordingEligibility(
                ReservationAnonymisationBlockerCode.ActiveDataHold),
            new RecordingApprovalGate(CreateApprovalEvidence()),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        Result<ReservationAnonymisationReceiptDto> blocked =
            await blockedHandler.HandleAsync(
                CreateCommand(blockedReservation),
                CancellationToken.None);
        Assert.Equal(
            ReservationsApplicationErrors.AnonymisationBlocked(
                ReservationAnonymisationBlockerCode.ActiveDataHold),
            blocked.Error);
        Assert.False(blockedReservation.IsAnonymised);
    }

    private static ApplyReservationAnonymisationCommand CreateCommand(
        Reservation reservation) => new(
        Guid.NewGuid(),
        reservation.PropertyId,
        Guid.NewGuid(),
        ApprovalRevision: 4,
        OperationRevision: 5,
        reservation.Id,
        reservation.Version,
        reservation.DetailsRevision,
        CreateRoutingEvidence(),
        "user:privacy-executor");

    private static ReservationAnonymisationRoutingPolicyEvidence
        CreateRoutingEvidence() => new(
        PropertyPolicySourceVersion: 7,
        OperatingCountryCode: "GB",
        PolicyId: "gb-hostel",
        PolicyVersion: 3,
        RetentionPolicyId: "reservation-operational",
        RetentionPolicyVersion: 2,
        ContentSha256: new string('a', 64),
        PurposeCode: "data-rights-anonymisation",
        Surface: "erasure",
        SourceProvenance: "authorized-workspace-operator",
        EvaluatedAtUtc: Now.AddMinutes(-5));

    private static DataRightsApprovalEvidence CreateApprovalEvidence()
    {
        ReservationAnonymisationRoutingPolicyEvidence routing =
            CreateRoutingEvidence();
        return new(
            SchemaVersion: 1,
            PropertyId: Guid.Empty,
            PropertyVersion: routing.PropertyPolicySourceVersion,
            routing.OperatingCountryCode,
            routing.PolicyId,
            routing.PolicyVersion,
            routing.RetentionPolicyId,
            routing.RetentionPolicyVersion,
            routing.ContentSha256,
            routing.PurposeCode,
            routing.Surface,
            routing.SourceProvenance,
            routing.EvaluatedAtUtc,
            RequiresDistinctExecutor: true);
    }

    private static Reservation CreateTerminalReservation()
    {
        Reservation reservation = Reservation.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            "Original Guest",
            "original@example.test",
            "+44 20 1234 5678",
            1,
            ReservationSource.Direct,
            null,
            null,
            "Late arrival",
            Guid.NewGuid(),
            Guid.NewGuid(),
            ReservationDetailsChangeOrigin.Staff,
            "user:creator",
            null,
            null,
            Guid.NewGuid(),
            Now.AddDays(-1),
            new TimeOnly(15, 0),
            new TimeOnly(11, 0)).Value;
        Assert.True(reservation.RejectAllocation(
            reservation.AllocationRequestId,
            ReservationAllocationRejection.UnitNotSellable,
            Guid.NewGuid(),
            Now.AddHours(-1)).IsSuccess);
        reservation.ClearDomainEvents();
        return reservation;
    }

    private sealed class StubReservationRepository(Reservation reservation)
        : IReservationRepository
    {
        public Task AddAsync(
            Reservation value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Reservation?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Reservation?>(null);

        public Task<Reservation?> GetForDataRightsAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Reservation?>(
                reservation.PropertyId == propertyId &&
                reservation.Id == reservationId
                    ? reservation
                    : null);

        public Task<Reservation?> GetAsyncByReservationId(
            Guid reservationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Reservation?> GetByExternalSourceAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExternalSourceExistsAsync(
            string sourceSystem,
            string sourceReference,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ReservationListResponse> ListAsync(
            Guid propertyId,
            IReadOnlyCollection<ReservationStatus>? statuses,
            string? search,
            ReservationListOrder order,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingAnonymisationRepository
        : IReservationAnonymisationRepository
    {
        public ReservationAnonymisationReceipt? Receipt { get; private set; }
        public int AddCount { get; private set; }

        public Task<ReservationAnonymisationReceipt?>
            FindReceiptByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Receipt?.IdempotencyKey == idempotencyKey
                    ? this.Receipt
                    : null);

        public Task<ReservationAnonymisationAffectedRecords>
            RedactOwnedRecordsAsync(
                Reservation reservation,
                ReservationAnonymisationOutcome outcome,
                CancellationToken cancellationToken) =>
            Task.FromResult(new ReservationAnonymisationAffectedRecords(
                RedactedHistoryCount: 2,
                ReducedExternalOperationCount: 1,
                SuppressedReminderCount: 1));

        public Task<bool> VerifyOwnerStateAsync(
            ReservationAnonymisationReceipt receipt,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Receipt == receipt);

        public Task AddReceiptAsync(
            ReservationAnonymisationReceipt receipt,
            CancellationToken cancellationToken)
        {
            this.Receipt = receipt;
            this.AddCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOperationLock : IReservationOperationLock
    {
        public List<Guid> ReservationIds { get; } = [];

        public Task AcquireAsync(
            string tenantId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            Assert.Equal("tenant-a", tenantId);
            this.ReservationIds.Add(reservationId);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEligibility(
        ReservationAnonymisationBlockerCode blocker =
            ReservationAnonymisationBlockerCode.None)
        : IReservationAnonymisationEligibilityEvaluator
    {
        public int CallCount { get; private set; }

        public Task<ReservationAnonymisationEligibilityResult> EvaluateAsync(
            ReservationAnonymisationEligibilityRequest request,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            bool eligible =
                blocker == ReservationAnonymisationBlockerCode.None;
            return Task.FromResult(new ReservationAnonymisationEligibilityResult(
                ReservationAnonymisationEligibilityContract.CurrentVersion,
                eligible
                    ? ReservationAnonymisationEligibilityStatus.Eligible
                    : ReservationAnonymisationEligibilityStatus.Blocked,
                blocker,
                request.SelectedReservationVersion,
                request.SelectedDetailsRevision,
                ActiveHoldCount: 0,
                eligible ? new string('b', 64) : null,
                Now));
        }
    }

    private sealed class RecordingApprovalGate(
        DataRightsApprovalEvidence? evidence)
        : IDataRightsOperationApprovalGate
    {
        public int CallCount { get; private set; }

        public Task<DataRightsOperationApprovalResult> EvaluateAsync(
            DataRightsOperationApprovalRequest request,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            DataRightsApprovalEvidence? approvedEvidence = evidence is null
                ? null
                : evidence with { PropertyId = request.PropertyId };
            return Task.FromResult(approvedEvidence is null
                ? DataRightsOperationApprovalResult.Denied(
                    DataRightsOperationApprovalDenial.CaseNotApproved)
                : DataRightsOperationApprovalResult.ApprovedWithEvidence(
                    approvedEvidence));
        }
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
        public Guid NewId() => Guid.CreateVersion7();
    }
}
