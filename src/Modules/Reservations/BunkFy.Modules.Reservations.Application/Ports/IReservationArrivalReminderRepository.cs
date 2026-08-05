namespace BunkFy.Modules.Reservations.Application.Ports;

using System.Runtime.CompilerServices;

public interface IReservationArrivalReminderRepository
{
    Task ApplyPropertyAsync(
        ReservationReminderPropertyWriteModel property,
        CancellationToken cancellationToken);

    Task RefreshReservationAsync(
        ReservationReminderSource reservation,
        CancellationToken cancellationToken);

    Task SuppressForProcessingRestrictionAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    Task<ReservationArrivalReminderClaimResult> ClaimDueAsync(
        DateTimeOffset nowUtc,
        int batchSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> ListScheduleScopeIdsAsync(CancellationToken cancellationToken);

    async IAsyncEnumerable<string> StreamScheduleScopeIdsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<string> scopeIds =
            await this.ListScheduleScopeIdsAsync(cancellationToken)
                .ConfigureAwait(false);
        foreach (string scopeId in scopeIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return scopeId;
        }
    }
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
