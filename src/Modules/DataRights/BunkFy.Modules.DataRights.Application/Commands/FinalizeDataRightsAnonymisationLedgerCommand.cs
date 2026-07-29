namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Application.Models;
using Gma.Framework.Cqrs;

internal sealed record FinalizeDataRightsAnonymisationLedgerCommand(
    Guid WorkItemId,
    Guid CaseId,
    DataRightsCaseScope Scope,
    long ApprovalRevision,
    long ExecutionRevision,
    Guid TaskRunId)
    : ITransactionalCommand<Unit>;
