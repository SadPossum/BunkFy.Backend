namespace BunkFy.Modules.Ingestion.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Runs;

internal sealed class AdvanceConnectionCheckpointCommandHandler(
    IAdapterConnectionRepository connections,
    IIngestionRunRepository runs,
    ISystemClock clock)
    : ICommandHandler<AdvanceConnectionCheckpointCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        AdvanceConnectionCheckpointCommand command,
        CancellationToken cancellationToken)
    {
        AdapterConnection? connection = await connections.GetAsync(command.ConnectionId, cancellationToken)
            .ConfigureAwait(false);
        if (connection is null)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.ConnectionNotFound);
        }

        IngestionRun? run = await runs.GetAsync(command.RunId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.RunNotFound);
        }

        if (run.ConnectionId != connection.Id || run.PropertyId != connection.PropertyId)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.RunConnectionMismatch);
        }

        if (run.State != IngestionRunState.Running)
        {
            return Result.Failure<Unit>(IngestionApplicationErrors.RunNotActive);
        }

        Result advanced;
        if (run.ExecutionKind == IngestionRunExecutionKind.RemoteLease)
        {
            if (command.RemoteLease is null || !command.RemoteCredentialId.HasValue ||
                command.RemoteLease.RunId != run.Id)
            {
                return Result.Failure<Unit>(
                    BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseMismatch);
            }

            advanced = connection.AdvanceRemoteCheckpoint(
                run.Id,
                command.RemoteLease.LeaseId,
                command.RemoteLease.LeaseEpoch,
                command.RemoteCredentialId.Value,
                command.RemoteLease.WorkerId,
                command.Checkpoint,
                connection.Version,
                clock.UtcNow);
        }
        else
        {
            if (command.RemoteLease is not null || command.RemoteCredentialId.HasValue)
            {
                return Result.Failure<Unit>(IngestionApplicationErrors.AdapterCompletionMismatch);
            }

            advanced = connection.AdvanceCheckpoint(command.Checkpoint, connection.Version, clock.UtcNow);
        }
        return advanced.IsSuccess
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(advanced.Error);
    }
}
