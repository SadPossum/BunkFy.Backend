namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationAnonymisationRepository(
    ReservationsDbContext dbContext)
    : IReservationAnonymisationRepository,
        IReservationAnonymisationRestoreRepository
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public Task<ReservationAnonymisationReceipt?> FindReceiptByIdempotencyKeyAsync(
        Guid idempotencyKey,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationReceipts
            .SingleOrDefaultAsync(
                receipt => receipt.IdempotencyKey == idempotencyKey,
                cancellationToken);

    public Task<ReservationAnonymisationAffectedRecords>
        RedactOwnedRecordsAsync(
            Reservation reservation,
            ReservationAnonymisationOutcome outcome,
            CancellationToken cancellationToken) =>
        this.RedactOwnedRecordsCoreAsync(
            reservation,
            new(
                outcome.EventId,
                outcome.PreviousDetailsRevision,
                outcome.CurrentDetailsRevision,
                outcome.ActorId,
                outcome.ChangedFields,
                outcome.CompletedAtUtc),
            cancellationToken);

    public Task<ReservationAnonymisationAffectedRecords>
        RedactRestoredOwnedRecordsAsync(
            Reservation reservation,
            ReservationAnonymisationRestoreOutcome? outcome,
            CancellationToken cancellationToken) =>
        this.RedactOwnedRecordsCoreAsync(
            reservation,
            outcome is null
                ? null
                : new(
                    outcome.EventId,
                    outcome.PreviousDetailsRevision,
                    outcome.CurrentDetailsRevision,
                    outcome.ActorId,
                    outcome.ChangedFields,
                    outcome.ReplayedAtUtc),
            cancellationToken);

    private async Task<ReservationAnonymisationAffectedRecords>
        RedactOwnedRecordsCoreAsync(
            Reservation reservation,
            ReservationAnonymisationHistoryAppend? historyAppend,
            CancellationToken cancellationToken)
    {
        ReservationDetailsHistoryEntry[] history = await dbContext
            .ReservationDetailsHistory
            .Where(entry =>
                entry.PropertyId == reservation.PropertyId &&
                entry.ReservationId == reservation.Id)
            .OrderBy(entry => entry.ToRevision)
            .ThenBy(entry => entry.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (ReservationDetailsHistoryEntry entry in history)
        {
            string? beforeSnapshot = RedactSnapshot(entry.BeforeSnapshotJson);
            string afterSnapshot = RedactSnapshot(entry.AfterSnapshotJson)!;
            entry.RedactSnapshots(
                beforeSnapshot,
                afterSnapshot,
                Hash(afterSnapshot));
        }

        if (historyAppend is not null)
        {
            ReservationDetailsSnapshot redactedSnapshot = new(
                reservation.Arrival,
                reservation.Departure,
                reservation.RequestedUnits
                    .Select(unit => unit.InventoryUnitId)
                    .ToArray(),
                Reservation.AnonymisedGuestName,
                email: null,
                phone: null,
                reservation.GuestCount,
                notes: null,
                reservation.ExpectedArrivalTime,
                reservation.ExpectedDepartureTime);
            string redactedJson = JsonSerializer.Serialize(
                redactedSnapshot,
                SerializerOptions);
            dbContext.ReservationDetailsHistory.Add(
                new ReservationDetailsHistoryEntry(
                    historyAppend.EventId,
                    reservation.ScopeId,
                    reservation.Id,
                    reservation.PropertyId,
                    historyAppend.PreviousDetailsRevision,
                    historyAppend.CurrentDetailsRevision,
                    ReservationDetailsChangeOrigin.DataRightsAnonymisation,
                    historyAppend.ActorId,
                    adapterConnectionId: null,
                    externalOperationId: null,
                    $"event:{historyAppend.EventId:N}",
                    historyAppend.EventId,
                    JsonSerializer.Serialize(
                        historyAppend.ChangedFields,
                        SerializerOptions),
                    redactedJson,
                    redactedJson,
                    Hash(redactedJson),
                    historyAppend.ChangedAtUtc));
        }

        ReservationExternalOperation[] externalOperations = await dbContext
            .ExternalOperations
            .Where(operation =>
                operation.PropertyId == reservation.PropertyId &&
                operation.ReservationId == reservation.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (ReservationExternalOperation operation in externalOperations)
        {
            operation.ReduceToReconciliationProof();
        }

        ReservationArrivalReminder[] reminders = await dbContext.ArrivalReminders
            .Where(reminder =>
                reminder.PropertyId == reservation.PropertyId &&
                reminder.ReservationId == reservation.Id &&
                reminder.State == ReservationArrivalReminderState.Pending)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (ReservationArrivalReminder reminder in reminders)
        {
            reminder.Supersede();
        }

        return new(
            history.Length + (historyAppend is null ? 0 : 1),
            externalOperations.Length,
            reminders.Length);
    }

    public Task<Reservation?> GetReservationAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.Reservations.SingleOrDefaultAsync(
            reservation =>
                reservation.PropertyId == propertyId &&
                reservation.Id == reservationId,
            cancellationToken);

    public Task<ReservationAnonymisationReceipt?>
        GetOriginalReceiptAsync(
            Guid receiptId,
            CancellationToken cancellationToken) =>
        dbContext.AnonymisationReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.Id == receiptId,
                cancellationToken);

    public Task<ReservationAnonymisationTombstone?> GetTombstoneAsync(
        Guid reservationId,
        CancellationToken cancellationToken) =>
        dbContext.AnonymisationTombstones
            .SingleOrDefaultAsync(
                tombstone => tombstone.Id == reservationId,
                cancellationToken);

    public Task<ReservationAnonymisationRestoreReceipt?>
        GetRestoreReceiptAsync(
            Guid ledgerEntryId,
            CancellationToken cancellationToken) =>
        dbContext.AnonymisationRestoreReceipts
            .AsNoTracking()
            .SingleOrDefaultAsync(
                receipt => receipt.LedgerEntryId == ledgerEntryId,
                cancellationToken);

    public async Task<bool> VerifyOwnerStateAsync(
        ReservationAnonymisationReceipt receipt,
        CancellationToken cancellationToken)
    {
        ReservationAnonymisationTombstone? tombstone =
            await dbContext.AnonymisationTombstones
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.Id == receipt.ReservationId,
                    cancellationToken)
                .ConfigureAwait(false);
        bool hasGuestLinks = await dbContext.ReservationGuests.AsNoTracking()
            .AnyAsync(
                guest => guest.ReservationId == receipt.ReservationId,
                cancellationToken)
            .ConfigureAwait(false);
        bool hasPendingReminder = await dbContext.ArrivalReminders.AsNoTracking()
            .AnyAsync(
                reminder =>
                    reminder.PropertyId == receipt.PropertyId &&
                    reminder.ReservationId == receipt.ReservationId &&
                    reminder.State == ReservationArrivalReminderState.Pending,
                cancellationToken)
            .ConfigureAwait(false);
        bool hasUnreducedOperation = await dbContext.ExternalOperations
            .AsNoTracking()
            .AnyAsync(
                operation =>
                    operation.PropertyId == receipt.PropertyId &&
                    operation.ReservationId == receipt.ReservationId &&
                    (operation.RequestFingerprint !=
                        ReservationExternalOperation.RedactedRequestFingerprint ||
                     operation.ErrorCode != null),
                cancellationToken)
            .ConfigureAwait(false);
        ReservationDetailsHistoryEntry[] history = await dbContext
            .ReservationDetailsHistory
            .AsNoTracking()
            .Where(entry =>
                entry.PropertyId == receipt.PropertyId &&
                entry.ReservationId == receipt.ReservationId)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return tombstone is not null &&
            tombstone.Matches(receipt) &&
            !hasGuestLinks &&
            !hasPendingReminder &&
            !hasUnreducedOperation &&
            history.Length == receipt.RedactedHistoryCount &&
            history.All(IsRedacted);
    }

    public Task AddOwnerProofAsync(
        ReservationAnonymisationReceipt receipt,
        ReservationAnonymisationTombstone tombstone,
        CancellationToken cancellationToken)
    {
        dbContext.AnonymisationReceipts.Add(receipt);
        dbContext.AnonymisationTombstones.Add(tombstone);
        return Task.CompletedTask;
    }

    public async Task<bool> VerifyRestoredOwnerStateAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        bool hasGuestLinks = await dbContext.ReservationGuests
            .AsNoTracking()
            .AnyAsync(
                guest => guest.ReservationId == reservationId,
                cancellationToken)
            .ConfigureAwait(false);
        bool hasPendingReminder = await dbContext.ArrivalReminders
            .AsNoTracking()
            .AnyAsync(
                reminder =>
                    reminder.PropertyId == propertyId &&
                    reminder.ReservationId == reservationId &&
                    reminder.State ==
                        ReservationArrivalReminderState.Pending,
                cancellationToken)
            .ConfigureAwait(false);
        bool hasUnreducedOperation = await dbContext.ExternalOperations
            .AsNoTracking()
            .AnyAsync(
                operation =>
                    operation.PropertyId == propertyId &&
                    operation.ReservationId == reservationId &&
                    (operation.RequestFingerprint !=
                        ReservationExternalOperation
                            .RedactedRequestFingerprint ||
                     operation.ErrorCode != null),
                cancellationToken)
            .ConfigureAwait(false);
        ReservationDetailsHistoryEntry[] history =
            await dbContext.ReservationDetailsHistory
                .AsNoTracking()
                .Where(entry =>
                    entry.PropertyId == propertyId &&
                    entry.ReservationId == reservationId)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        return !hasGuestLinks &&
            !hasPendingReminder &&
            !hasUnreducedOperation &&
            history.All(IsRedacted);
    }

    public Task AddRestoreProofAsync(
        ReservationAnonymisationRestoreReceipt receipt,
        ReservationAnonymisationTombstone? newTombstone,
        CancellationToken cancellationToken)
    {
        if (newTombstone is not null)
        {
            dbContext.AnonymisationTombstones.Add(newTombstone);
        }

        dbContext.AnonymisationRestoreReceipts.Add(receipt);
        return Task.CompletedTask;
    }

    private static bool IsRedacted(ReservationDetailsHistoryEntry entry)
    {
        string? before = RedactSnapshot(entry.BeforeSnapshotJson);
        string? after = RedactSnapshot(entry.AfterSnapshotJson);
        return string.Equals(
                before,
                entry.BeforeSnapshotJson,
                StringComparison.Ordinal) &&
            string.Equals(after, entry.AfterSnapshotJson, StringComparison.Ordinal) &&
            string.Equals(
                Hash(entry.AfterSnapshotJson),
                entry.AfterSnapshotHash,
                StringComparison.Ordinal);
    }

    private static string? RedactSnapshot(string? json)
    {
        if (json is null)
        {
            return null;
        }

        ReservationDetailsSnapshot? snapshot =
            JsonSerializer.Deserialize<ReservationDetailsSnapshot>(
                json,
                SerializerOptions);
        return snapshot is null
            ? throw new InvalidOperationException(
                "Reservation details history contains an invalid snapshot.")
            : JsonSerializer.Serialize(
                CreateRedactedSnapshot(snapshot),
                SerializerOptions);
    }

    private static ReservationDetailsSnapshot CreateRedactedSnapshot(
        ReservationDetailsSnapshot snapshot) => new(
        snapshot.Arrival,
        snapshot.Departure,
        snapshot.InventoryUnitIds,
        Reservation.AnonymisedGuestName,
        email: null,
        phone: null,
        snapshot.GuestCount,
        notes: null,
        snapshot.ExpectedArrivalTime,
        snapshot.ExpectedDepartureTime);

    private static string Hash(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record ReservationAnonymisationHistoryAppend(
        Guid EventId,
        long PreviousDetailsRevision,
        long CurrentDetailsRevision,
        string ActorId,
        IReadOnlyCollection<string> ChangedFields,
        DateTimeOffset ChangedAtUtc);
}
