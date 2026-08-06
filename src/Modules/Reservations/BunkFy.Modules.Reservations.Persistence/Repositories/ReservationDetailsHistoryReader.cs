namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using System.Text.Json;
using Gma.Framework.Pagination;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class ReservationDetailsHistoryReader(ReservationsDbContext dbContext)
    : IReservationDetailsHistoryReader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<ReservationDetailsOperationReplay?> FindOperationAsync(
        Guid propertyId,
        Guid reservationId,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        ReservationDetailsHistoryEntry? entry = await dbContext.ReservationDetailsHistory
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.PropertyId == propertyId &&
                    item.ReservationId == reservationId &&
                    item.CorrelationId == correlationId,
                cancellationToken)
            .ConfigureAwait(false);
        return entry is null
            ? null
            : new(
                entry.FromRevision,
                (ReservationDetailsChangeOriginKind)(int)entry.Origin,
                Deserialize<ReservationDetailsSnapshotDto>(entry.AfterSnapshotJson));
    }

    public async Task<ReservationDetailsHistoryListResponse> ListAsync(
        Guid propertyId,
        Guid reservationId,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        ReservationDetailsHistoryEntry[] entries = await dbContext.ReservationDetailsHistory
            .AsNoTracking()
            .Where(entry => entry.PropertyId == propertyId && entry.ReservationId == reservationId)
            .OrderByDescending(entry => entry.ToRevision)
            .ThenByDescending(entry => entry.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize + 1)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        bool hasMore = entries.Length > pageRequest.PageSize;
        return new(
            entries.Take(pageRequest.PageSize).Select(Map).ToArray(),
            pageRequest.Page,
            pageRequest.PageSize,
            hasMore);
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
