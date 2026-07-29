namespace BunkFy.Modules.Guests.Application.Commands;

using BunkFy.Modules.Retention.Contracts;

internal sealed record GuestRetentionExecutionStart(
    bool DispatchRequired,
    long StartingProjectionOrdinal,
    int AffectedCount,
    RetentionContributionResult? CompletedResult);
