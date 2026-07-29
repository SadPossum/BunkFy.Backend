namespace BunkFy.Modules.Reservations.Application.Commands;

using BunkFy.Modules.Retention.Contracts;

internal sealed record ReservationRetentionExecutionStart(
    bool DispatchRequired,
    long StartingProjectionOrdinal,
    int AffectedCount,
    RetentionContributionResult? CompletedResult);
