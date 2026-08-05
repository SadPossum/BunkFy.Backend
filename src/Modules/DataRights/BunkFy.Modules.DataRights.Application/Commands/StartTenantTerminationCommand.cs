namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record StartTenantTerminationCommand(
    Guid CaseId,
    Guid ProcessId,
    TenantTerminationApprovalEvidence ApprovalEvidence,
    long ExpectedCaseVersion,
    string ActorId) : ITransactionalCommand<TenantTerminationStartDto>;
