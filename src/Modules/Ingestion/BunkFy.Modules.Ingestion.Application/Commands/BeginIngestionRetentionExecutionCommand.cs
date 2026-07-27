namespace BunkFy.Modules.Ingestion.Application.Commands;

using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;

public sealed record BeginIngestionRetentionExecutionCommand(
    RetentionContributionRequest Request)
    : ITransactionalCommand<IngestionRetentionExecutionStart>;
