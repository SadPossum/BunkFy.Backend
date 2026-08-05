namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class BeginTenantTerminationExportArtifactGenerationCommandHandler(
    ITenantTerminationRepository repository,
    ITenantTerminationExportFragmentRepository fragments,
    ITenantTerminationExportArtifactRepository artifacts,
    ITenantTerminationCoordinationSignal coordinationSignal,
    ISystemClock clock)
    : ICommandHandler<
        BeginTenantTerminationExportArtifactGenerationCommand,
        TenantTerminationExportArtifactGenerationStart>
{
    public async Task<Result<TenantTerminationExportArtifactGenerationStart>>
        HandleAsync(
            BeginTenantTerminationExportArtifactGenerationCommand command,
            CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await repository.GetProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        if (process is null ||
            process.Phase != TenantTerminationProcessPhase.Export ||
            process.Status != TenantTerminationProcessStatus.Running ||
            process.OperationRevision != command.OperationRevision ||
            command.TaskRunId != TenantTerminationExecutionIdentity
                .CreateExportArtifactTaskRunId(
                    command.ProcessId,
                    command.OperationRevision) ||
            command.TaskAttempt <= 0)
        {
            return Invalid();
        }

        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems =
            await repository.ListOwnerWorkItemsAsync(
                process.Id,
                TenantTerminationOwnerPhase.Export,
                process.OperationRevision,
                cancellationToken).ConfigureAwait(false);
        IReadOnlyList<TenantTerminationExportFragment> exactFragments =
            await fragments.ListAsync(
                process.Id,
                process.OperationRevision,
                cancellationToken).ConfigureAwait(false);
        if (exactFragments.Count == 0)
        {
            return Invalid();
        }

        Guid artifactId = TenantTerminationExecutionIdentity
            .CreateExportArtifactId(process.Id, process.OperationRevision);
        Guid idempotencyKey = TenantTerminationExecutionIdentity
            .CreateExportArtifactIdempotencyKey(
                process.Id,
                process.OperationRevision);
        DateTimeOffset nowUtc = clock.UtcNow;
        DateTimeOffset expiresAtUtc = exactFragments.Min(fragment =>
            fragment.ExpiresAtUtc);
        Result<TenantTerminationExportArtifact> prepared =
            TenantTerminationExportArtifactCoordinator.Prepare(
                process,
                workItems,
                exactFragments,
                artifactId,
                idempotencyKey,
                nowUtc,
                expiresAtUtc);
        if (prepared.IsFailure)
        {
            return Result.Failure<
                TenantTerminationExportArtifactGenerationStart>(
                    prepared.Error);
        }

        TenantTerminationExportArtifact? artifact =
            await artifacts.GetByProcessAsync(
                process.Id,
                process.OperationRevision,
                cancellationToken).ConfigureAwait(false);
        if (artifact is null)
        {
            TenantTerminationExportArtifact? byKey =
                await artifacts.GetByIdempotencyKeyAsync(
                    idempotencyKey,
                    cancellationToken).ConfigureAwait(false);
            if (byKey is not null)
            {
                return Invalid();
            }

            artifact = prepared.Value;
            await artifacts.AddAsync(
                artifact,
                cancellationToken).ConfigureAwait(false);
        }
        else if (artifact.Id != artifactId ||
            !artifact.Matches(
                process.Id,
                process.OperationRevision,
                idempotencyKey,
                process.FrozenRevisionSha256!,
                prepared.Value.FragmentSetSha256))
        {
            return Invalid();
        }

        if (process.HasCurrentExportConfirmation() &&
            !MatchesConfirmation(process, artifact))
        {
            return Invalid();
        }

        if (artifact.State == TenantTerminationExportArtifactState.Available)
        {
            if (!process.HasCurrentExportConfirmation())
            {
                long previousVersion = process.Version;
                Result confirmed = TenantTerminationExportArtifactCoordinator
                    .Confirm(
                        process,
                        artifact,
                        process.Version,
                        TenantTerminationCoordination.ExecutorActorId,
                        nowUtc);
                if (confirmed.IsFailure)
                {
                    return Result.Failure<
                        TenantTerminationExportArtifactGenerationStart>(
                            confirmed.Error);
                }

                if (process.Version != previousVersion)
                {
                    _ = await coordinationSignal.EnqueueAsync(
                        process,
                        process.LastChangedAtUtc,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return Result.Success(
                new TenantTerminationExportArtifactGenerationStart(
                DispatchRequired: false,
                process.Version,
                artifact.Version,
                artifact,
                exactFragments));
        }

        Result begun = artifact.BeginGeneration(
            command.TaskRunId,
            command.TaskAttempt,
            nowUtc);
        return begun.IsSuccess
            ? Result.Success(
                new TenantTerminationExportArtifactGenerationStart(
                DispatchRequired: true,
                process.Version,
                artifact.Version,
                artifact,
                exactFragments))
            : Result.Failure<TenantTerminationExportArtifactGenerationStart>(
                begun.Error);
    }

    private static bool MatchesConfirmation(
        TenantTerminationProcess process,
        TenantTerminationExportArtifact artifact) =>
        artifact.State == TenantTerminationExportArtifactState.Available &&
        process.ExportArtifactId == artifact.Id &&
        process.ExportArtifactVersion == artifact.Version &&
        string.Equals(
            process.ExportFrozenRevisionSha256,
            artifact.FrozenRevisionSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            process.ExportFragmentSetSha256,
            artifact.FragmentSetSha256,
            StringComparison.Ordinal);

    private static Result<TenantTerminationExportArtifactGenerationStart>
        Invalid() =>
        Result.Failure<TenantTerminationExportArtifactGenerationStart>(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid);
}
