namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class ReservationDetailsHistoryReader(ReservationsDbContext dbContext)
    : IReservationDetailsHistoryReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ReservationDetailsHistoryItem>> ListAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        ReservationDetailsHistoryEntry[] entries = await dbContext.ReservationDetailsHistory
            .AsNoTracking()
            .Where(entry => entry.PropertyId == propertyId && entry.ReservationId == reservationId)
            .OrderBy(entry => entry.ToRevision)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return entries.Select(Map).ToArray();
    }

    private static ReservationDetailsHistoryItem Map(ReservationDetailsHistoryEntry entry) => new(
        entry.Id,
        entry.ReservationId,
        entry.PropertyId,
        entry.FromRevision,
        entry.ToRevision,
        (ReservationDetailsChangeOriginKind)(int)entry.Origin,
        entry.ActorId,
        entry.AdapterConnectionId,
        entry.ExternalOperationId,
        entry.CorrelationId,
        Deserialize<string[]>(entry.ChangedFieldsJson),
        entry.BeforeSnapshotJson is null
            ? null
            : Deserialize<ReservationDetailsSnapshotDto>(entry.BeforeSnapshotJson),
        Deserialize<ReservationDetailsSnapshotDto>(entry.AfterSnapshotJson),
        entry.OccurredAtUtc);

    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, SerializerOptions) ??
        throw new InvalidOperationException("Reservation details history contains invalid JSON.");
}
