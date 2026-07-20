namespace BunkFy.Modules.Reservations.Application.Ports;

public interface IReservationArrivalReminderRepository
{
    Task ApplyPropertyAsync(
        ReservationReminderPropertyWriteModel property,
        CancellationToken cancellationToken);

    Task RefreshReservationAsync(
        ReservationReminderSource reservation,
        CancellationToken cancellationToken);

    Task<ReservationArrivalReminderClaimResult> ClaimDueAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListScheduleScopeIdsAsync(CancellationToken cancellationToken);
}

public sealed record ReservationReminderPropertyWriteModel(
    string ScopeId,
    Guid PropertyId,
    string? TimeZoneId,
    bool IsActive,
    long SourceVersion,
    DateTimeOffset OccurredAtUtc);

public sealed record ReservationReminderSource(
    string ScopeId,
    Guid ReservationId,
    Guid PropertyId,
    DateOnly Arrival,
    TimeOnly? ExpectedArrivalTime,
    long DetailsRevision);

public sealed record ReservationArrivalReminderDispatch(
    Guid ReminderId,
    string ScopeId,
    Guid ReservationId,
    Guid PropertyId,
    DateOnly Arrival,
    TimeOnly ExpectedArrivalTime,
    string TimeZoneId,
    long DetailsRevision);

public sealed record ReservationArrivalReminderClaimResult(
    int ProcessedCount,
    IReadOnlyList<ReservationArrivalReminderDispatch> Dispatches);
