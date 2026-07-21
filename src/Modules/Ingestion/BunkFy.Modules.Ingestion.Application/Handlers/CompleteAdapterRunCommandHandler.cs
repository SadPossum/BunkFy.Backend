namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Runs;

internal sealed class CompleteAdapterRunCommandHandler(
    IIngestionRunRepository runs,
    ISystemClock clock)
    : ICommandHandler<CompleteAdapterRunCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        CompleteAdapterRunCommand command,
        CancellationToken cancellationToken)
    {
        IngestionRun? run = await runs.GetAsync(command.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.RunNotFound);
        }

        if (run.TaskRunId != command.TaskRunId || run.TaskAttempt != command.TaskAttempt)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.TaskContextMismatch);
        }

        Result completed = run.Complete(
            command.Outcome,
            command.ObservedCount,
            command.AcceptedCount,
            command.RejectedCount,
            command.AcceptedCheckpoint,
            command.ErrorCode,
            run.Version,
            clock.UtcNow);
        return completed.IsSuccess
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(completed.Error);
    }
}
