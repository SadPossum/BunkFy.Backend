namespace BunkFy.Modules.DataRights.Application.Commands;

using Gma.Framework.Cqrs;

internal sealed record FinalizeDataRightsAnonymisationLedgerCommand(
    Guid WorkItemId,
    Guid CaseId,
    Guid PropertyId,
    long ApprovalRevision,
    long ExecutionRevision,
    Guid TaskRunId)
    : ITransactionalCommand<Unit>;
