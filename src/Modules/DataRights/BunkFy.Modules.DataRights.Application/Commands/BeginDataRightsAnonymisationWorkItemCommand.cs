namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using Gma.Framework.Cqrs;

internal sealed record BeginDataRightsAnonymisationWorkItemCommand(
    Guid WorkItemId,
    Guid CaseId,
    DataRightsCaseScope Scope,
    long ApprovalRevision,
    long ExecutionRevision,
    Guid TaskRunId,
    int TaskAttempt)
    : ITransactionalCommand<DataRightsAnonymisationWorkItemStart>;
