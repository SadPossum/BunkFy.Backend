namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record RecoverTenantTerminationCommand(
    Guid CaseId,
    Guid ProcessId,
    TenantTerminationApprovalEvidence ApprovalEvidence,
    long ExpectedCaseVersion,
    long? ExpectedProcessVersion,
    string ActorId) : ITransactionalCommand<TenantTerminationStartDto>;
