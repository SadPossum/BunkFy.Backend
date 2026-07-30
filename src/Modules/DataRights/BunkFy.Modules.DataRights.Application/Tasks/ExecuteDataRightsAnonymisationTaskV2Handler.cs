namespace BunkFy.Modules.DataRights.Application.Tasks;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;

internal sealed class ExecuteDataRightsAnonymisationTaskV2Handler(
    ITaskCommandDispatcher commandDispatcher,
    IEnumerable<IDataRightsAnonymisationContributorV2> contributors,
    ISystemClock clock,
    IEnumerable<IDataRightsAnonymisationExecutionPrerequisiteV2>?
        prerequisites = null)
    : ITaskHandler<ExecuteDataRightsAnonymisationPayloadV2>
{
    private readonly DataRightsAnonymisationTaskExecutor executor =
        new(
            commandDispatcher,
            [],
            contributors,
            clock,
            prerequisites);

    public Task HandleAsync(
        ExecuteDataRightsAnonymisationPayloadV2 payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (!DataRightsCaseScope.TryCreate(
                payload.CaseType,
                payload.PropertyId,
                out DataRightsCaseScope? scope) ||
            scope is null ||
            !MatchesScope(payload))
        {
            throw new InvalidOperationException(
                "DataRights.AnonymisationTaskScopeInvalid");
        }

        return this.executor.ExecuteAsync(
            payload.WorkItemId,
            payload.CaseId,
            scope,
            payload.ApprovalRevision,
            payload.ExecutionRevision,
            context,
            cancellationToken);
    }

    private static bool MatchesScope(
        ExecuteDataRightsAnonymisationPayloadV2 payload) =>
        payload.ScopeKind switch
        {
            DataRightsExecutionScopeKind.Property =>
                payload.PropertyId.HasValue,
            DataRightsExecutionScopeKind.Tenant =>
                payload.PropertyId is null,
            _ => false
        };
}
