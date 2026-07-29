namespace BunkFy.Modules.DataRights.Application.Tasks;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;

internal sealed class ExecuteDataRightsAnonymisationTaskHandler(
    ITaskCommandDispatcher commandDispatcher,
    IEnumerable<IDataRightsAnonymisationContributor> contributors,
    ISystemClock clock)
    : ITaskHandler<ExecuteDataRightsAnonymisationPayload>
{
    private readonly DataRightsAnonymisationTaskExecutor executor =
        new(
            commandDispatcher,
            contributors,
            [],
            clock);

    public Task HandleAsync(
        ExecuteDataRightsAnonymisationPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken) =>
        this.executor.ExecuteAsync(
            payload.WorkItemId,
            payload.CaseId,
            DataRightsCaseScope.ForProperty(payload.PropertyId),
            payload.ApprovalRevision,
            payload.ExecutionRevision,
            context,
            cancellationToken);
}
