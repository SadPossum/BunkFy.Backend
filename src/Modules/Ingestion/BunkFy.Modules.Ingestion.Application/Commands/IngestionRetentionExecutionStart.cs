namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Modules.Retention.Contracts;

public sealed record IngestionRetentionExecutionStart(
    bool DispatchRequired,
    RetentionContributionResult? CompletedResult);
