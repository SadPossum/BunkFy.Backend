namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Handlers;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using Gma.Framework.Application.Events;
using Gma.Framework.Domain;
using Gma.Framework.Pagination;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Xunit;
using DomainReviewReason = BunkFy.Modules.Reservations.Domain.GuestRecords.ReservationGuestRecordLinkReviewReason;

[Trait("Category", "Unit")]
public sealed class GuestProfileCreationLinkConfirmationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Matching_guest_creation_confirms_the_prepared_process()
    {
        ReservationGuestRecordLinkProcess process = CreateProcess();
        RecordingProjectionRepository projections = new();
        RecordingDomainEventDispatcher dispatched = new();
        GuestProfileCreatedProjectionHandler handler = new(
            projections,
            new FakeProcessRepository(process),
            ReservationMutationTestSupport.Create(new UnusedReservationRepository()),
            new ReservationInboxDomainEventDispatcher(dispatched),
            new TestClock(),
            new TestIdGenerator());

        await handler.HandleAsync(
            CreatedEvent(
                process.Id,
                process.PropertyId,
                process.CreationConfirmationId),
            CancellationToken.None);

        Assert.Equal(ReservationGuestRecordLinkProcessState.Ready, process.State);
        Assert.Equal(1, process.DispatchRevision);
        Assert.Single(dispatched.Events);
        Assert.Single(projections.Profiles);
        Assert.Single(projections.Restrictions);
    }

    [Fact]
    public async Task Correlation_mismatch_fails_closed_without_ready_dispatch()
    {
        ReservationGuestRecordLinkProcess process = CreateProcess();
        RecordingDomainEventDispatcher dispatched = new();
        GuestProfileCreatedProjectionHandler handler = new(
            new RecordingProjectionRepository(),
            new FakeProcessRepository(process),
            ReservationMutationTestSupport.Create(new UnusedReservationRepository()),
            new ReservationInboxDomainEventDispatcher(dispatched),
            new TestClock(),
            new TestIdGenerator());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(
                CreatedEvent(
                    Guid.NewGuid(),
                    process.PropertyId,
                    process.CreationConfirmationId),
                CancellationToken.None));

        Assert.Equal(
            "Reservations.GuestRecordLinkProcessConflict",
            exception.Message);
        Assert.Equal(ReservationGuestRecordLinkProcessState.Prepared, process.State);
        Assert.Empty(dispatched.Events);
    }

    [Fact]
    public async Task Guest_anonymisation_terminates_prepared_link_before_delayed_creation()
    {
        ReservationGuestRecordLinkProcess process = CreateProcess();
        FakeProcessRepository processes = new(process);
        RecordingProjectionRepository projections = new();
        GuestProfileAnonymisedProjectionHandler anonymised = new(
            projections,
            processes,
            new TestClock());

        await anonymised.HandleAsync(
            new GuestProfileAnonymisedIntegrationEvent(
                Guid.NewGuid(),
                "tenant-a",
                Now,
                process.Id,
                guestVersion: 2),
            CancellationToken.None);

        Assert.Equal(
            ReservationGuestRecordLinkProcessState.NeedsReview,
            process.State);
        Assert.Equal(
            DomainReviewReason.GuestUnavailable,
            process.ReviewReason);
        Assert.Equal(GuestStatus.Archived, Assert.Single(projections.Profiles).Status);

        RecordingDomainEventDispatcher dispatched = new();
        GuestProfileCreatedProjectionHandler created = new(
            projections,
            processes,
            ReservationMutationTestSupport.Create(new UnusedReservationRepository()),
            new ReservationInboxDomainEventDispatcher(dispatched),
            new TestClock(),
            new TestIdGenerator());
        await created.HandleAsync(
            CreatedEvent(
                process.Id,
                process.PropertyId,
                process.CreationConfirmationId),
            CancellationToken.None);

        Assert.Equal(
            ReservationGuestRecordLinkProcessState.NeedsReview,
            process.State);
        Assert.Empty(dispatched.Events);
    }

    private static GuestProfileCreatedIntegrationEvent CreatedEvent(
        Guid guestId,
        Guid propertyId,
        Guid confirmationId) => new(
        Guid.NewGuid(),
        "tenant-a",
        Now,
        guestId,
        propertyId,
        GuestStatus.Active,
        guestVersion: 1,
        creationConfirmationId: confirmationId);

    private static ReservationGuestRecordLinkProcess CreateProcess() =>
        ReservationGuestRecordLinkProcess.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            expectedReservationVersion: 1,
            "user:staff",
            Now).Value;

    private sealed class FakeProcessRepository(
        ReservationGuestRecordLinkProcess process)
        : IReservationGuestRecordLinkProcessRepository
    {
        public Task<ReservationGuestRecordLinkProcess?> GetByOperationAsync(
            Guid operationId,
            CancellationToken cancellationToken) => Task.FromResult(
            process.Id == operationId ? process : null);

        public Task<ReservationGuestRecordLinkProcess?> GetByReservationAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) => Task.FromResult(
            process.PropertyId == propertyId && process.ReservationId == reservationId
                ? process
                : null);

        public Task<ReservationGuestRecordLinkProcess?> GetByConfirmationAsync(
            Guid creationConfirmationId,
            CancellationToken cancellationToken) => Task.FromResult(
            process.CreationConfirmationId == creationConfirmationId
                ? process
                : null);

        public Task AddAsync(
            ReservationGuestRecordLinkProcess value,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingProjectionRepository
        : IReservationGuestProfileProjectionRepository
    {
        public List<ReservationGuestProfileProjectionWriteModel> Profiles { get; } = [];
        public List<ReservationGuestProcessingRestrictionProjectionWriteModel> Restrictions { get; } = [];

        public Task<bool> IsLinkableAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task ApplyAsync(
            ReservationGuestProfileProjectionWriteModel profile,
            CancellationToken cancellationToken)
        {
            this.Profiles.Add(profile);
            return Task.CompletedTask;
        }

        public Task ApplyRestrictionAsync(
            ReservationGuestProcessingRestrictionProjectionWriteModel restriction,
            CancellationToken cancellationToken)
        {
            this.Restrictions.Add(restriction);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDomainEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> Events { get; } = [];

        public Task DispatchAsync(
            IReadOnlyCollection<IDomainEvent> domainEvents,
            CancellationToken cancellationToken)
        {
            this.Events.AddRange(domainEvents);
            return Task.CompletedTask;
        }
    }

    private sealed class UnusedReservationRepository : IReservationRepository
    {
        public Task AddAsync(
            Reservation reservation,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Reservation?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Reservation?> GetForDataRightsAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Reservation?> GetAsyncByReservationId(
            Guid reservationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

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

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now.AddMinutes(1);
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.NewGuid();
    }
}
