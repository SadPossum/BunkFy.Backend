namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.Events;

internal sealed class ReservationDetailsHistoryWriter(ReservationsDbContext dbContext)
    : IReservationDetailsHistoryWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public Task AppendAsync(
        ReservationDetailsChangedDomainEvent change,
        CancellationToken cancellationToken)
    {
        string changedFields = JsonSerializer.Serialize(change.ChangedFields, SerializerOptions);
        string? before = change.Before is null
            ? null
            : JsonSerializer.Serialize(change.Before, SerializerOptions);
        string after = JsonSerializer.Serialize(change.After, SerializerOptions);
        string afterHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(after)));
        dbContext.Set<ReservationDetailsHistoryEntry>().Add(new ReservationDetailsHistoryEntry(
            change.EventId,
            change.ScopeId,
            change.ReservationId,
            change.PropertyId,
            change.FromRevision,
            change.ToRevision,
            change.Origin,
            change.ActorId,
            change.AdapterConnectionId,
            change.ExternalOperationId,
            change.ExternalOperationId.HasValue
                ? $"external:{change.ExternalOperationId.Value:N}"
                : $"event:{change.EventId:N}",
            change.CorrelationId,
            changedFields,
            before,
            after,
            afterHash,
            change.OccurredAtUtc));
        return Task.CompletedTask;
    }
}
