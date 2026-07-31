namespace BunkFy.Modules.Workspaces.Domain.Termination;

using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceTerminationFence : ScopedAggregateRoot<Guid>
{
    private WorkspaceTerminationFence() { }

    private WorkspaceTerminationFence(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid ProcessId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public Guid TerminationEpoch { get; private set; }
    public string PolicyEvidenceSha256 { get; private set; } = string.Empty;
    public WorkspaceTerminationFenceState State { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string LastChangedBy { get; private set; } = string.Empty;
    public DateTimeOffset LastChangedAtUtc { get; private set; }
    public long Version { get; private set; } = 1;

    public static Result<WorkspaceTerminationFence> Freeze(
        Guid id,
        string tenantId,
        Guid processId,
        Guid caseId,
        long approvalRevision,
        Guid terminationEpoch,
        string policyEvidenceSha256,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (id == Guid.Empty ||
            processId == Guid.Empty ||
            caseId == Guid.Empty ||
            approvalRevision < 1 ||
            terminationEpoch == Guid.Empty ||
            !WorkspaceTerminationFenceRules.IsSha256(
                policyEvidenceSha256) ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<WorkspaceTerminationFence>(
                WorkspaceTerminationFenceErrors.IdentityInvalid);
        }

        string actor = WorkspaceTerminationFenceRules.NormalizeActor(actorId);
        if (actor.Length == 0 || nowUtc == default)
        {
            return Result.Failure<WorkspaceTerminationFence>(
                WorkspaceTerminationFenceErrors.CoordinatesInvalid);
        }

        return Result.Success(new WorkspaceTerminationFence(id, scopeId)
        {
            ProcessId = processId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            TerminationEpoch = terminationEpoch,
            PolicyEvidenceSha256 = policyEvidenceSha256,
            State = WorkspaceTerminationFenceState.Frozen,
            CreatedBy = actor,
            CreatedAtUtc = nowUtc,
            LastChangedBy = actor,
            LastChangedAtUtc = nowUtc
        });
    }

    public Result BeginDestruction(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc) =>
        this.Transition(
            expectedVersion,
            WorkspaceTerminationFenceState.Frozen,
            WorkspaceTerminationFenceState.DestructionStarted,
            actorId,
            nowUtc);

    public Result Close(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc) =>
        this.Transition(
            expectedVersion,
            WorkspaceTerminationFenceState.DestructionStarted,
            WorkspaceTerminationFenceState.Closed,
            actorId,
            nowUtc);

    public Result Release(
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc) =>
        this.Transition(
            expectedVersion,
            WorkspaceTerminationFenceState.Frozen,
            WorkspaceTerminationFenceState.Released,
            actorId,
            nowUtc);

    private Result Transition(
        long expectedVersion,
        WorkspaceTerminationFenceState expectedState,
        WorkspaceTerminationFenceState nextState,
        string actorId,
        DateTimeOffset nowUtc)
    {
        if (expectedVersion != this.Version)
        {
            return Result.Failure(
                WorkspaceTerminationFenceErrors.VersionConflict);
        }

        string actor = WorkspaceTerminationFenceRules.NormalizeActor(actorId);
        if (this.State != expectedState ||
            nextState == WorkspaceTerminationFenceState.Unknown ||
            actor.Length == 0 ||
            nowUtc == default ||
            nowUtc < this.LastChangedAtUtc)
        {
            return Result.Failure(
                WorkspaceTerminationFenceErrors.TransitionInvalid);
        }

        this.State = nextState;
        this.LastChangedBy = actor;
        this.LastChangedAtUtc = nowUtc;
        this.Version++;
        return Result.Success();
    }
}
