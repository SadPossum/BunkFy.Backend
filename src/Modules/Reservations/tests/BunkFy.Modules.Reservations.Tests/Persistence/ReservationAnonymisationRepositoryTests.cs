namespace BunkFy.Modules.Reservations.Tests.Persistence;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using BunkFy.Modules.Reservations.Persistence;
using BunkFy.Modules.Reservations.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationAnonymisationRepositoryTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 2, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Redaction_is_atomic_owner_proof_and_hides_ordinary_record()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        Reservation reservation = CreateTerminalReservation();
        Guid guestId = Guid.NewGuid();
        Assert.True(reservation.LinkGuest(
            guestId,
            ReservationGuestRole.Primary,
            replaceExistingRole: false,
            reservation.Version,
            "staff:front-desk",
            Guid.NewGuid(),
            Now.AddHours(-3)).IsSuccess);
        Assert.True(reservation.RejectAllocation(
            reservation.AllocationRequestId,
            ReservationAllocationRejection.UnitNotSellable,
            Guid.NewGuid(),
            Now.AddHours(-2)).IsSuccess);
        reservation.ClearDomainEvents();
        dbContext.Reservations.Add(reservation);
        dbContext.GuestRecordLinkProcesses.Add(
            ReservationGuestRecordLinkProcess.Prepare(
                guestId,
                reservation.ScopeId,
                reservation.PropertyId,
                reservation.Id,
                Guid.NewGuid(),
                reservation.Version,
                "staff:front-desk",
                Now.AddHours(-2)).Value);

        string originalSnapshot = SerializeSnapshot(reservation);
        dbContext.ReservationDetailsHistory.Add(
            new ReservationDetailsHistoryEntry(
                Guid.NewGuid(),
                reservation.ScopeId,
                reservation.Id,
                reservation.PropertyId,
                fromRevision: 0,
                toRevision: reservation.DetailsRevision,
                ReservationDetailsChangeOrigin.Staff,
                actorId: "staff:front-desk",
                adapterConnectionId: null,
                externalOperationId: null,
                operationDeduplicationKey: new string('d', 64),
                Guid.NewGuid(),
                changedFieldsJson: "[\"PrimaryGuestName\",\"Email\",\"Phone\",\"Notes\"]",
                beforeSnapshotJson: originalSnapshot,
                afterSnapshotJson: originalSnapshot,
                afterSnapshotHash: Hash(originalSnapshot),
                Now.AddHours(-4)));
        dbContext.ExternalOperations.Add(
            new ReservationExternalOperation(
                new ReservationExternalOperationRecord(
                    Guid.NewGuid(),
                    reservation.ScopeId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    reservation.PropertyId,
                    ExternalReservationOperationKind.Amend,
                    new string('a', Reservation.RequestFingerprintLength),
                    ExternalReservationOperationOutcome.ValidationRejected,
                    reservation.Id,
                    reservation.DetailsRevision,
                    reservation.Version,
                    "Guest maya@example.test could not be applied",
                    Now.AddHours(-1))));
        dbContext.ArrivalReminders.Add(
            ReservationArrivalReminder.Create(
                Guid.NewGuid(),
                reservation.ScopeId,
                reservation.Id,
                reservation.PropertyId,
                reservation.DetailsRevision,
                "UTC",
                reservation.Arrival,
                reservation.ExpectedArrivalTime!.Value,
                Now.AddDays(4),
                Now.AddDays(4).AddHours(-2),
                leadTimeMinutes: 120));
        await dbContext.SaveChangesAsync();

        long selectedVersion = reservation.Version;
        long selectedDetailsRevision = reservation.DetailsRevision;
        ReservationAnonymisationOutcome outcome = reservation.Anonymise(
            selectedVersion,
            selectedDetailsRevision,
            "user:privacy-executor",
            Guid.NewGuid(),
            Now).Value;
        ReservationAnonymisationRepository anonymisation = new(dbContext);
        ReservationAnonymisationAffectedRecords affected =
            await anonymisation.RedactOwnedRecordsAsync(
                reservation,
                outcome,
                CancellationToken.None);
        ReservationAnonymisationReceipt receipt =
            ReservationAnonymisationReceipt.Create(
                Guid.NewGuid(),
                reservation.ScopeId,
                Guid.NewGuid(),
                reservation.PropertyId,
                Guid.NewGuid(),
                approvalRevision: 1,
                operationRevision: 2,
                reservation.Id,
                outcome,
                affected.RedactedHistoryCount,
                affected.ReducedExternalOperationCount,
                affected.SuppressedReminderCount,
                new string('b', ReservationAnonymisationReceipt.Sha256Length),
                new string('c', ReservationAnonymisationReceipt.Sha256Length)).Value;
        ReservationAnonymisationTombstone tombstone =
            ReservationAnonymisationTombstone.Create(receipt).Value;
        await anonymisation.AddOwnerProofAsync(
            receipt,
            tombstone,
            CancellationToken.None);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        Assert.Empty(await dbContext.ReservationGuests.ToArrayAsync());
        Assert.Empty(await dbContext.GuestRecordLinkProcesses.ToArrayAsync());
        ReservationDetailsHistoryEntry[] history = await dbContext
            .ReservationDetailsHistory
            .OrderBy(entry => entry.ToRevision)
            .ToArrayAsync();
        Assert.Equal(2, history.Length);
        Assert.All(history, entry =>
        {
            Assert.DoesNotContain(
                "Original Guest",
                entry.AfterSnapshotJson,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "maya@example.test",
                entry.AfterSnapshotJson,
                StringComparison.Ordinal);
            Assert.Contains(
                Reservation.AnonymisedGuestName,
                entry.AfterSnapshotJson,
                StringComparison.Ordinal);
            Assert.Equal(Hash(entry.AfterSnapshotJson), entry.AfterSnapshotHash);
        });
        ReservationExternalOperation externalOperation =
            Assert.Single(await dbContext.ExternalOperations.ToArrayAsync());
        Assert.Equal(
            ReservationExternalOperation.RedactedRequestFingerprint,
            externalOperation.RequestFingerprint);
        Assert.Null(externalOperation.ErrorCode);
        Assert.Equal(
            ReservationArrivalReminderState.Superseded,
            Assert.Single(await dbContext.ArrivalReminders.ToArrayAsync()).State);

        ReservationAnonymisationReceipt storedReceipt =
            Assert.Single(await dbContext.AnonymisationReceipts.ToArrayAsync());
        Assert.True(await anonymisation.VerifyOwnerStateAsync(
            storedReceipt,
            CancellationToken.None));
        ReservationRepository reservations = new(
            dbContext,
            new NoOpRestrictionProjectionRepository());
        Assert.Null(await reservations.GetAsync(
            reservation.PropertyId,
            reservation.Id,
            CancellationToken.None));
        Assert.NotNull(await reservations.GetForDataRightsAsync(
            reservation.PropertyId,
            reservation.Id,
            CancellationToken.None));

        dbContext.AnonymisationReceipts.Remove(storedReceipt);
        InvalidOperationException mutationError =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => dbContext.SaveChangesAsync());
        Assert.Contains("append-only", mutationError.Message);
    }

    [Fact]
    public async Task Restore_re_scrubs_child_stores_without_duplicate_history()
    {
        await using ReservationsDbContext dbContext = CreateDbContext();
        Reservation reservation = CreateTerminalReservation();
        ReservationAnonymisationRestoreOutcome restored =
            reservation.RestoreAnonymisation(
                reservation.Version + 1,
                "data-rights-restore",
                Guid.NewGuid(),
                Now.AddHours(-2),
                Now.AddHours(-1)).Value;
        reservation.ClearDomainEvents();
        dbContext.Reservations.Add(reservation);

        string staleSnapshot = JsonSerializer.Serialize(
            new ReservationDetailsSnapshot(
                reservation.Arrival,
                reservation.Departure,
                reservation.RequestedUnits
                    .Select(unit => unit.InventoryUnitId)
                    .ToArray(),
                "Restored Guest",
                "restored@example.test",
                "+44 20 1111 2222",
                reservation.GuestCount,
                "Restored note",
                reservation.ExpectedArrivalTime,
                reservation.ExpectedDepartureTime),
            SerializerOptions);
        dbContext.ReservationDetailsHistory.Add(
            new ReservationDetailsHistoryEntry(
                Guid.NewGuid(),
                reservation.ScopeId,
                reservation.Id,
                reservation.PropertyId,
                fromRevision: 0,
                toRevision: restored.CurrentDetailsRevision,
                ReservationDetailsChangeOrigin.Staff,
                actorId: "staff:restore",
                adapterConnectionId: null,
                externalOperationId: null,
                operationDeduplicationKey: new string('e', 64),
                Guid.NewGuid(),
                changedFieldsJson: "[\"PrimaryGuestName\",\"Email\"]",
                beforeSnapshotJson: staleSnapshot,
                afterSnapshotJson: staleSnapshot,
                afterSnapshotHash: Hash(staleSnapshot),
                Now.AddHours(-3)));
        dbContext.ExternalOperations.Add(
            new ReservationExternalOperation(
                new ReservationExternalOperationRecord(
                    Guid.NewGuid(),
                    reservation.ScopeId,
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    reservation.PropertyId,
                    ExternalReservationOperationKind.Amend,
                    new string('f', Reservation.RequestFingerprintLength),
                    ExternalReservationOperationOutcome.ValidationRejected,
                    reservation.Id,
                    reservation.DetailsRevision,
                    reservation.Version,
                    "Restored Guest could not be applied",
                    Now.AddHours(-2))));
        dbContext.ArrivalReminders.Add(
            ReservationArrivalReminder.Create(
                Guid.NewGuid(),
                reservation.ScopeId,
                reservation.Id,
                reservation.PropertyId,
                reservation.DetailsRevision,
                "UTC",
                reservation.Arrival,
                reservation.ExpectedArrivalTime!.Value,
                Now.AddDays(4),
                Now.AddDays(4).AddHours(-2),
                leadTimeMinutes: 120));
        await dbContext.SaveChangesAsync();

        ReservationAnonymisationRepository anonymisation = new(dbContext);
        ReservationAnonymisationAffectedRecords affected =
            await anonymisation.RedactRestoredOwnedRecordsAsync(
                reservation,
                outcome: null,
                CancellationToken.None);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        Assert.Equal(1, affected.RedactedHistoryCount);
        Assert.Equal(1, affected.ReducedExternalOperationCount);
        Assert.Equal(1, affected.SuppressedReminderCount);
        ReservationDetailsHistoryEntry history = Assert.Single(
            await dbContext.ReservationDetailsHistory.ToArrayAsync());
        Assert.DoesNotContain(
            "Restored Guest",
            history.AfterSnapshotJson,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "restored@example.test",
            history.AfterSnapshotJson,
            StringComparison.Ordinal);
        Assert.True(await anonymisation.VerifyRestoredOwnerStateAsync(
            reservation.PropertyId,
            reservation.Id,
            CancellationToken.None));
    }

    private static Reservation CreateTerminalReservation() => Reservation.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        Guid.NewGuid(),
        new DateOnly(2026, 8, 1),
        new DateOnly(2026, 8, 3),
        [Guid.NewGuid()],
        "Original Guest",
        "maya@example.test",
        "+44 20 1234 5678",
        guestCount: 1,
        ReservationSource.External,
        sourceSystem: "booking.com",
        sourceReference: "booking-reference",
        notes: "Late arrival",
        Guid.NewGuid(),
        Guid.NewGuid(),
        ReservationDetailsChangeOrigin.Adapter,
        initialDetailsActorId: "adapter:booking",
        initialAdapterConnectionId: Guid.NewGuid(),
        initialExternalOperationId: Guid.NewGuid(),
        Guid.NewGuid(),
        Now.AddDays(-1),
        expectedArrivalTime: new TimeOnly(15, 0),
        expectedDepartureTime: new TimeOnly(11, 0)).Value;

    private static string SerializeSnapshot(Reservation reservation) =>
        JsonSerializer.Serialize(
            new ReservationDetailsSnapshot(
                reservation.Arrival,
                reservation.Departure,
                reservation.RequestedUnits
                    .Select(unit => unit.InventoryUnitId)
                    .ToArray(),
                reservation.PrimaryGuestName,
                reservation.Email,
                reservation.Phone,
                reservation.GuestCount,
                reservation.Notes,
                reservation.ExpectedArrivalTime,
                reservation.ExpectedDepartureTime),
            SerializerOptions);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static ReservationsDbContext CreateDbContext()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase(
                    $"reservations-anonymisation-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext("tenant-a"));
    }

    private sealed class NoOpRestrictionProjectionRepository
        : IReservationProcessingRestrictionProjectionRepository
    {
        public Task<ReservationProcessingRestrictionProjection?> GetAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ReservationProcessingRestrictionProjection?>(null);

        public Task EnsureAsync(
            string tenantId,
            Guid propertyId,
            Guid reservationId,
            DateTimeOffset initializedAtUtc,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
