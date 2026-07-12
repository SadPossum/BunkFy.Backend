namespace BunkFy.Modules.Reservations.Contracts;

public sealed record ReservationDetailsHistoryItem(
    Guid ChangeId,
    Guid ReservationId,
    Guid PropertyId,
    long FromRevision,
    long ToRevision,
    ReservationDetailsChangeOriginKind Origin,
    string? ActorId,
    Guid? AdapterConnectionId,
    Guid? ExternalOperationId,
    Guid CorrelationId,
    IReadOnlyCollection<string> ChangedFields,
    ReservationDetailsSnapshotDto? Before,
    ReservationDetailsSnapshotDto After,
    DateTimeOffset OccurredAtUtc);
