namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class FailTenantTerminationExportFragmentGenerationCommandHandler(
    ITenantTerminationRepository repository,
    ITenantTerminationExportFragmentRepository fragments,
    ISystemClock clock)
    : ICommandHandler<FailTenantTerminationExportFragmentGenerationCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(
        FailTenantTerminationExportFragmentGenerationCommand command,
        CancellationToken cancellationToken)
    {
        TenantTerminationProcess? process = await repository.GetProcessAsync(
            command.ProcessId,
            cancellationToken).ConfigureAwait(false);
        TenantTerminationExportFragment? fragment =
            await fragments.GetAsync(
                command.WorkItemId,
                cancellationToken).ConfigureAwait(false);
        if (process is null ||
            fragment is null ||
            process.OperationRevision != command.OperationRevision ||
            fragment.ProcessId != process.Id ||
            fragment.ExportOperationRevision != process.OperationRevision ||
            fragment.Version != command.ExpectedFragmentVersion ||
            fragment.GenerationRunId != command.TaskRunId ||
            fragment.GenerationAttempt != command.TaskAttempt)
        {
            return Result.Failure<Unit>(
                DataRightsApplicationErrors
                    .TenantTerminationExecutionStateInvalid);
        }

        Result failed = fragment.MarkFailed(
            command.TaskRunId,
            command.TaskAttempt,
            command.FailureCode,
            clock.UtcNow);
        return failed.IsSuccess
            ? Result.Success(Unit.Value)
            : Result.Failure<Unit>(failed.Error);
    }
}
