namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class CompleteTenantTerminationExportArtifactGenerationCommandHandler(
    ITenantTerminationRepository repository,
    ITenantTerminationExportArtifactRepository artifacts,
    ITenantTerminationCoordinationSignal coordinationSignal,
    ISystemClock clock)
    : ICommandHandler<
        CompleteTenantTerminationExportArtifactGenerationCommand,
        TenantTerminationExportArtifactGenerationCompleted>
{
    public async Task<Result<TenantTerminationExportArtifactGenerationCompleted>>
        HandleAsync(
            CompleteTenantTerminationExportArtifactGenerationCommand command,
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
            command.ProtectedArtifact is null ||
            process.Version != command.ExpectedProcessVersion ||
            process.Phase != TenantTerminationProcessPhase.Export ||
            process.Status != TenantTerminationProcessStatus.Running ||
            process.OperationRevision != command.OperationRevision ||
            artifact.Version != command.ExpectedArtifactVersion ||
            artifact.ProcessId != process.Id ||
            artifact.ExportOperationRevision != process.OperationRevision ||
            artifact.GenerationRunId != command.TaskRunId ||
            artifact.GenerationAttempt != command.TaskAttempt)
        {
            return Invalid();
        }

        Result available = artifact.MarkAvailable(
            command.TaskRunId,
            command.TaskAttempt,
            command.ProtectedArtifact.FragmentCount,
            command.ProtectedArtifact.RecordCount,
            command.ProtectedArtifact.FragmentSetSha256,
            command.ProtectedArtifact.StorageKey,
            command.ProtectedArtifact.EncryptedByteLength,
            command.ProtectedArtifact.PlaintextSha256,
            command.ProtectedArtifact.EncryptionKeyVersion,
            command.ProtectedArtifact.FormatVersion,
            command.ProtectedArtifact.AvailableAtUtc,
            command.ProtectedArtifact.ExpiresAtUtc);
        if (available.IsFailure)
        {
            return Result.Failure<
                TenantTerminationExportArtifactGenerationCompleted>(
                    available.Error);
        }

        Result confirmed = TenantTerminationExportArtifactCoordinator.Confirm(
            process,
            artifact,
            command.ExpectedProcessVersion,
            TenantTerminationCoordination.ExecutorActorId,
            clock.UtcNow);
        if (confirmed.IsFailure)
        {
            return Result.Failure<
                TenantTerminationExportArtifactGenerationCompleted>(
                    confirmed.Error);
        }

        _ = await coordinationSignal.EnqueueAsync(
            process,
            process.LastChangedAtUtc,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(new
            TenantTerminationExportArtifactGenerationCompleted(
                process.Id,
                process.Version,
                artifact.Id,
                artifact.Version));
    }

    private static Result<TenantTerminationExportArtifactGenerationCompleted>
        Invalid() =>
        Result.Failure<TenantTerminationExportArtifactGenerationCompleted>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}
