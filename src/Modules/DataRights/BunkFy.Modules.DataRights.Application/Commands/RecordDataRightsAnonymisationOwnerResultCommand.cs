namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

internal sealed record RecordDataRightsAnonymisationOwnerResultCommand(
    Guid WorkItemId,
    Guid CaseId,
    DataRightsCaseScope Scope,
    long ApprovalRevision,
    long ExecutionRevision,
    Guid TaskRunId,
    int TaskAttempt,
    long ExpectedWorkItemVersion,
    DataRightsAnonymisationContributionResult Result)
    : ITransactionalCommand<Unit>;
