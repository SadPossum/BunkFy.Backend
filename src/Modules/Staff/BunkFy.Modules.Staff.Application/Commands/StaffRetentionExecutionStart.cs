namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.Retention.Contracts;

internal sealed record StaffRetentionExecutionStart(
    bool DispatchRequired,
    long StartingProjectionOrdinal,
    int AffectedCount,
    RetentionContributionResult? CompletedResult);
