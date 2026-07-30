namespace BunkFy.Modules.Workspaces.Application.Queries;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Cqrs;

public sealed record
    GetWorkspaceStaffOnboardingDataRightsCorrectionTargetQuery(
        Guid ExecutionId,
        Guid CaseId,
        long ApprovalRevision,
        Guid ApplicationId,
        long ExpectedVersion,
        string ActorId)
    : IQuery<WorkspaceStaffOnboardingDataRightsCorrectionTargetDto>;
