namespace BunkFy.Modules.Retention.Application.Commands;

using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

public sealed record CompleteRetentionExecutionCommand(
    Guid ExecutionId,
    int Attempt,
    RetentionContributionResult Result)
    : ITransactionalCommand<Unit>;
