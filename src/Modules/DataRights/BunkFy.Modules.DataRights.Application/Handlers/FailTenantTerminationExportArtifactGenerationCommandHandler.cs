namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class FailTenantTerminationExportArtifactGenerationCommandHandler(
    ITenantTerminationRepository repository,
    ITenantTerminationExportArtifactRepository artifacts,
    ISystemClock clock)
    : ICommandHandler<FailTenantTerminationExportArtifactGenerationCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        FailTenantTerminationExportArtifactGenerationCommand command,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await repository.GetProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        TenantTerminationExportArtifact? artifact = await artifacts.GetAsync(
            command.ArtifactId,
            cancellationToken).ConfigureAwait(false);
        if (process is null ||
            artifact is null ||
            process.OperationRevision != command.OperationRevision ||
            artifact.ProcessId != process.Id ||
            artifact.ExportOperationRevision != process.OperationRevision ||
            artifact.Version != command.ExpectedArtifactVersion ||
            artifact.GenerationRunId != command.TaskRunId ||
            artifact.GenerationAttempt != command.TaskAttempt)
        {
            return Result.Failure<Unit>(
                DataRightsApplicationErrors
                    .TenantTerminationExecutionStateInvalid);
        }

        Result failed = artifact.MarkFailed(
            command.TaskRunId,
            command.TaskAttempt,
            command.FailureCode,
            clock.UtcNow);
        return failed.IsSuccess
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(failed.Error);
    }
}
