namespace BunkFy.Modules.DataRights.Application.Commands;

using Gma.Framework.Cqrs;

internal sealed record BeginDataRightsAnonymisationWorkItemCommand(
    Guid WorkItemId,
    Guid CaseId,
    Guid PropertyId,
    long ApprovalRevision,
    long ExecutionRevision,
    Guid TaskRunId,
    int TaskAttempt)
    : ITransactionalCommand<DataRightsAnonymisationWorkItemStart>;
