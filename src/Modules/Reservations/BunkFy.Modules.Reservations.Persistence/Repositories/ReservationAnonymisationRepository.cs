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
    : IReservationAnonymisationRepository
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

    public async Task<ReservationAnonymisationAffectedRecords>
        RedactOwnedRecordsAsync(
            Reservation reservation,
            ReservationAnonymisationOutcome outcome,
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
                outcome.EventId,
                reservation.ScopeId,
                reservation.Id,
                reservation.PropertyId,
                outcome.PreviousDetailsRevision,
                outcome.CurrentDetailsRevision,
                ReservationDetailsChangeOrigin.DataRightsAnonymisation,
                outcome.ActorId,
                adapterConnectionId: null,
                externalOperationId: null,
                $"event:{outcome.EventId:N}",
                outcome.EventId,
                JsonSerializer.Serialize(
                    outcome.ChangedFields,
                    SerializerOptions),
                redactedJson,
                redactedJson,
                Hash(redactedJson),
                outcome.CompletedAtUtc));

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
            history.Length + 1,
            externalOperations.Length,
            reminders.Length);
    }

    public async Task<bool> VerifyOwnerStateAsync(
        ReservationAnonymisationReceipt receipt,
        CancellationToken cancellationToken)
    {
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
        return !hasGuestLinks &&
            !hasPendingReminder &&
            !hasUnreducedOperation &&
            history.Length == receipt.RedactedHistoryCount &&
            history.All(IsRedacted);
    }

    public Task AddReceiptAsync(
        ReservationAnonymisationReceipt receipt,
        CancellationToken cancellationToken)
    {
        dbContext.AnonymisationReceipts.Add(receipt);
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
}
