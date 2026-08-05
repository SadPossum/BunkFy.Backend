namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record DecideTenantTerminationCommand(
    Guid CaseId,
    DataRightsDecisionOutcome Decision,
    DataRightsDecisionReason Reason,
    TenantTerminationApprovalEvidence? ApprovalEvidence,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<TenantTerminationCaseDto>;
