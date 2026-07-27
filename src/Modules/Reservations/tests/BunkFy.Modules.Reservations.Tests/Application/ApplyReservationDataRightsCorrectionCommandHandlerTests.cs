namespace BunkFy.Modules.Reservations.Tests.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.Events;
using Gma.Framework.Messaging;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ApplyReservationDataRightsCorrectionCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Exact_approval_updates_details_and_prepares_pii_free_receipt()
    {
        Reservation reservation = CreateReservation();
        RecordingReceiptRepository receipts = new();
        RecordingApprovalGate approval = new();
        RecordingCountryPolicy countryPolicy = new();
        long initialVersion = reservation.Version;
        long initialDetailsRevision = reservation.DetailsRevision;
        ApplyReservationDataRightsCorrectionCommand command =
            CreateCommand(reservation, "Corrected Guest");
        Guid correlationId = Guid.NewGuid();
        Guid detailsEventId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        Guid receiptEventId = Guid.NewGuid();
        Guid completionEventId = Guid.NewGuid();
        ApplyReservationDataRightsCorrectionCommandHandler handler = CreateHandler(
            reservation,
            receipts,
            approval,
            countryPolicy,
            new QueueIdGenerator(
                correlationId,
                detailsEventId,
                receiptId,
                receiptEventId,
                completionEventId));

        Result<ReservationDataRightsCorrectionReceiptDto> result =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal("Corrected Guest", reservation.PrimaryGuestName);
        Assert.Equal(initialVersion + 1, reservation.Version);
        Assert.Equal(initialDetailsRevision + 1, reservation.DetailsRevision);
        Assert.Equal(receiptId, result.Value.ReceiptId);
        Assert.Equal(detailsEventId, result.Value.DetailsChangeEventId);
        Assert.Equal(receiptEventId, result.Value.EventId);
        Assert.Equal(["reservation.guest.primary-name"], result.Value.ChangedFields);
        Assert.Equal(
            ReservationCountryPolicyAdmission.DataRightsCorrectionPurpose,
            countryPolicy.PurposeCode);
        Assert.Equal(command.CaseId, approval.Request!.CaseId);
        Assert.Equal(command.IdempotencyKey, approval.Request.ExecutionId);
        Assert.Equal(command.ExpectedVersion, approval.Request.Coordinate.RecordVersion);
        Assert.Equal(command.ReservationId, approval.Request.Coordinate.RecordId);
        Assert.Equal(
            ReservationsDataRightsCoordinates.CorrectionFieldPolicyKey,
            approval.Request.FieldPolicyKey);
        Assert.Equal(command.ActorId, approval.Request.ExecutingActorId);
        Assert.NotNull(receipts.Added);
        Assert.Equal(correlationId, receipts.Added.CorrelationId);
        Assert.Equal(receiptEventId, receipts.Added.EventId);
        Assert.Equal(completionEventId, receipts.Added.CompletionEventId);
        Assert.DoesNotContain(
            typeof(ReservationDataRightsCorrectionReceipt).GetProperties(),
            property => property.Name is
                nameof(Reservation.PrimaryGuestName) or
                nameof(Reservation.Email) or
                nameof(Reservation.Phone) or
                nameof(Reservation.Notes) or
                nameof(Reservation.LastDetailsActorId));
    }

    [Fact]
    public async Task Approval_denial_and_no_change_leave_no_receipt()
    {
        Reservation deniedReservation = CreateReservation();
        long deniedVersion = deniedReservation.Version;
        RecordingReceiptRepository deniedReceipts = new();
        ApplyReservationDataRightsCorrectionCommandHandler deniedHandler = CreateHandler(
            deniedReservation,
            deniedReceipts,
            new RecordingApprovalGate(isApproved: false),
            new RecordingCountryPolicy(),
            new QueueIdGenerator());

        Result<ReservationDataRightsCorrectionReceiptDto> denied =
            await deniedHandler.HandleAsync(
                CreateCommand(deniedReservation, "Corrected Guest"),
                CancellationToken.None);

        Assert.Equal(ReservationsApplicationErrors.DataRightsApprovalRequired, denied.Error);
        Assert.Equal("Original Guest", deniedReservation.PrimaryGuestName);
        Assert.Equal(deniedVersion, deniedReservation.Version);
        Assert.Null(deniedReceipts.Added);

        Reservation unchangedReservation = CreateReservation();
        long unchangedVersion = unchangedReservation.Version;
        RecordingReceiptRepository unchangedReceipts = new();
        ApplyReservationDataRightsCorrectionCommandHandler unchangedHandler = CreateHandler(
            unchangedReservation,
            unchangedReceipts,
            new RecordingApprovalGate(),
            new RecordingCountryPolicy(),
            new QueueIdGenerator(Guid.NewGuid(), Guid.NewGuid()));

        Result<ReservationDataRightsCorrectionReceiptDto> unchanged =
            await unchangedHandler.HandleAsync(
                CreateCommand(unchangedReservation, unchangedReservation.PrimaryGuestName),
                CancellationToken.None);

        Assert.Equal(ReservationsApplicationErrors.CorrectionNoChanges, unchanged.Error);
        Assert.Equal(unchangedVersion, unchangedReservation.Version);
        Assert.Null(unchangedReceipts.Added);
    }

    [Fact]
    public async Task Replay_requires_the_exact_committed_payload_and_revisions()
    {
        Reservation reservation = CreateReservation();
        ApplyReservationDataRightsCorrectionCommand command =
            CreateCommand(reservation, "Corrected Guest");
        long committedVersion = reservation.Version + 1;
        ReservationDataRightsCorrectionReceipt receipt =
            ApplyAndCreateReceipt(reservation, command);
        RecordingApprovalGate approval = new();
        ApplyReservationDataRightsCorrectionCommandHandler handler = CreateHandler(
            reservation,
            new RecordingReceiptRepository(receipt),
            approval,
            new RecordingCountryPolicy(),
            new QueueIdGenerator());

        Result<ReservationDataRightsCorrectionReceiptDto> replay =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<ReservationDataRightsCorrectionReceiptDto> changedPayload =
            await handler.HandleAsync(
                command with { PrimaryGuestName = "Different Guest" },
                CancellationToken.None);

        Assert.True(replay.IsSuccess, replay.Error.Code);
        Assert.Equal(receipt.Id, replay.Value.ReceiptId);
        Assert.Equal(
            ReservationsApplicationErrors.CorrectionIdempotencyConflict,
            changedPayload.Error);
        Assert.Null(approval.Request);
        Assert.Equal("Corrected Guest", reservation.PrimaryGuestName);
        Assert.Equal(committedVersion, reservation.Version);
    }

    [Fact]
    public async Task Receipt_domain_event_projects_one_pii_free_outbox_event()
    {
        Reservation reservation = CreateReservation();
        ApplyReservationDataRightsCorrectionCommand command =
            CreateCommand(reservation, "Corrected Guest");
        ReservationDataRightsCorrectionReceipt receipt =
            ApplyAndCreateReceipt(reservation, command);
        ReservationDataRightsCorrectionAppliedDomainEvent domainEvent =
            Assert.IsType<ReservationDataRightsCorrectionAppliedDomainEvent>(
                Assert.Single(receipt.DomainEvents));
        RecordingOutbox outbox = new();

        await new ReservationDataRightsCorrectionAppliedOutboxProjector(
                new RecordingOutboxRegistry(outbox))
            .HandleAsync(domainEvent, CancellationToken.None);

        ReservationDataRightsCorrectionAppliedIntegrationEvent integrationEvent =
            Assert.Single(outbox.Events.OfType<
                ReservationDataRightsCorrectionAppliedIntegrationEvent>());
        DataRightsCorrectionAppliedIntegrationEvent completion =
            Assert.Single(outbox.Events.OfType<DataRightsCorrectionAppliedIntegrationEvent>());
        Assert.Equal(domainEvent.EventId, integrationEvent.EventId);
        Assert.Equal(domainEvent.DetailsChangeEventId, integrationEvent.DetailsChangeEventId);
        Assert.Equal(["reservation.guest.primary-name"], integrationEvent.ChangedFields);
        Assert.Equal(domainEvent.CompletionEventId, completion.EventId);
        Assert.Equal(command.IdempotencyKey, completion.ExecutionId);
        Assert.Equal(
            ReservationsDataRightsCoordinates.CorrectionFieldPolicyKey,
            completion.FieldPolicyKey);
        Assert.Equal(["reservation.guest.primary-name"], completion.ChangedFieldKeys);
        Assert.DoesNotContain(
            outbox.Events.SelectMany(item => item.GetType().GetProperties()),
            property => property.Name is
                "PrimaryGuestName" or "Email" or "Phone" or "Notes" or "ActorId");
    }

    private static ApplyReservationDataRightsCorrectionCommandHandler CreateHandler(
        Reservation reservation,
        RecordingReceiptRepository receipts,
        RecordingApprovalGate approval,
        RecordingCountryPolicy countryPolicy,
        IIdGenerator ids) => new(
        new RecordingReservationRepository(reservation),
        receipts,
        approval,
        countryPolicy,
        new TestScopeContext(),
        new TestClock(),
        ids);

    private static ApplyReservationDataRightsCorrectionCommand CreateCommand(
        Reservation reservation,
        string primaryGuestName) => new(
        Guid.NewGuid(),
        reservation.PropertyId,
        Guid.NewGuid(),
        ApprovalRevision: 3,
        reservation.Id,
        reservation.Version,
        reservation.DetailsRevision,
        primaryGuestName,
        reservation.Email,
        reservation.Phone,
        reservation.GuestCount,
        reservation.Notes,
        reservation.ExpectedArrivalTime,
        reservation.ExpectedDepartureTime,
        "user:privacy-operator");

    private static ReservationDataRightsCorrectionReceipt ApplyAndCreateReceipt(
        Reservation reservation,
        ApplyReservationDataRightsCorrectionCommand command)
    {
        Result<ReservationDataRightsCorrectionOutcome> correction =
            reservation.CorrectGuestDetails(
                command.PrimaryGuestName,
                command.Email,
                command.Phone,
                command.GuestCount,
                command.Notes,
                command.ExpectedVersion,
                command.ExpectedDetailsRevision,
                command.ActorId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now,
                command.ExpectedArrivalTime,
                command.ExpectedDepartureTime);
        Assert.True(correction.IsSuccess, correction.Error.Code);
        return ReservationDataRightsCorrectionReceipt.Create(
            Guid.NewGuid(),
            reservation.ScopeId,
            command.IdempotencyKey,
            command.PropertyId,
            command.CaseId,
            command.ApprovalRevision,
            command.ReservationId,
            correction.Value,
            Guid.NewGuid(),
            Guid.NewGuid()).Value;
    }

    private static Reservation CreateReservation() => Reservation.Create(
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
        guestCount: 1,
        ReservationSource.Direct,
        sourceSystem: null,
        sourceReference: null,
        notes: "Late arrival",
        eventId: Guid.NewGuid(),
        detailsEventId: Guid.NewGuid(),
        initialDetailsOrigin: ReservationDetailsChangeOrigin.Staff,
        initialDetailsActorId: "user:creator",
        initialAdapterConnectionId: null,
        initialExternalOperationId: null,
        initialCorrelationId: Guid.NewGuid(),
        nowUtc: Now.AddDays(-1),
        expectedArrivalTime: new TimeOnly(15, 0),
        expectedDepartureTime: new TimeOnly(11, 0)).Value;

    private sealed class RecordingReservationRepository(Reservation reservation)
        : IReservationRepository
    {
        public Task AddAsync(Reservation value, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Reservation?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<Reservation?>(
                reservation.PropertyId == propertyId && reservation.Id == reservationId
                    ? reservation
                    : null);

        public Task<Reservation?> GetForDataRightsAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            this.GetAsync(propertyId, reservationId, cancellationToken);

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

    private sealed class RecordingReceiptRepository(
        ReservationDataRightsCorrectionReceipt? existing = null)
        : IReservationDataRightsCorrectionReceiptRepository
    {
        public ReservationDataRightsCorrectionReceipt? Added { get; private set; }

        public Task<ReservationDataRightsCorrectionReceipt?> FindByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(existing?.IdempotencyKey == idempotencyKey ? existing : null);

        public Task AddAsync(
            ReservationDataRightsCorrectionReceipt receipt,
            CancellationToken cancellationToken)
        {
            this.Added = receipt;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingApprovalGate(bool isApproved = true)
        : IDataRightsCorrectionExecutionGate
    {
        public DataRightsCorrectionExecutionGateRequest? Request { get; private set; }

        public Task<DataRightsCorrectionExecutionGateResult> EvaluateAsync(
            DataRightsCorrectionExecutionGateRequest request,
            CancellationToken cancellationToken)
        {
            this.Request = request;
            return Task.FromResult(isApproved
                ? DataRightsCorrectionExecutionGateResult.Allowed(Now.AddMinutes(5))
                : DataRightsCorrectionExecutionGateResult.Denied(
                    DataRightsCorrectionExecutionDenial.CaseNotExecuting));
        }
    }

    private sealed class RecordingCountryPolicy : IReservationCountryPolicyAdmission
    {
        public string? PurposeCode { get; private set; }

        public Task<CountryPolicyDecision> EvaluateAsync(
            Guid propertyId,
            string purposeCode,
            CountryPolicySurface surface,
            string sourceProvenance,
            CancellationToken cancellationToken)
        {
            this.PurposeCode = purposeCode;
            return Task.FromResult(CountryPolicyDecision.Allow(new CountryPolicyEvidence(
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
    }

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => ReservationsModuleMetadata.Name;
        public List<IIntegrationEvent> Events { get; } = [];

        public Task EnqueueAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxRegistry(RecordingOutbox outbox)
        : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName) => outbox;
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

    private sealed class QueueIdGenerator(params Guid[] ids) : IIdGenerator
    {
        private readonly Queue<Guid> values = new(ids);

        public Guid NewId() => this.values.Dequeue();
    }
}
