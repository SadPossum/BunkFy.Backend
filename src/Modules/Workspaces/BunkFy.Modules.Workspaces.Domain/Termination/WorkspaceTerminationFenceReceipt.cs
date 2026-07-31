namespace BunkFy.Modules.Workspaces.Domain.Termination;

using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceTerminationFenceReceipt
    : ScopedAggregateRoot<Guid>
{
    private WorkspaceTerminationFenceReceipt() { }

    private WorkspaceTerminationFenceReceipt(Guid id, string scopeId)
        : base(id, scopeId)
    {
    }

    public Guid FenceId { get; private set; }
    public Guid ProcessId { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public Guid WorkItemId { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public Guid TerminationEpoch { get; private set; }
    public WorkspaceTerminationFenceAction Action { get; private set; }
    public long SelectedFenceVersion { get; private set; }
    public long ResultingFenceVersion { get; private set; }
    public WorkspaceTerminationFenceState ResultingState { get; private set; }
    public string PolicyEvidenceSha256 { get; private set; } = string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static Result<WorkspaceTerminationFenceReceipt> Create(
        Guid id,
        string tenantId,
        Guid fenceId,
        Guid processId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid workItemId,
        Guid idempotencyKey,
        Guid terminationEpoch,
        WorkspaceTerminationFenceAction action,
        long selectedFenceVersion,
        long resultingFenceVersion,
        WorkspaceTerminationFenceState resultingState,
        string policyEvidenceSha256,
        string actorId,
        DateTimeOffset completedAtUtc)
    {
        if (id == Guid.Empty ||
            fenceId == Guid.Empty ||
            processId == Guid.Empty ||
            caseId == Guid.Empty ||
            workItemId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            terminationEpoch == Guid.Empty ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Result.Failure<WorkspaceTerminationFenceReceipt>(
                WorkspaceTerminationFenceErrors.ReceiptIdentityInvalid);
        }

        string actor = WorkspaceTerminationFenceRules.NormalizeActor(actorId);
        if (approvalRevision < 1 ||
            operationRevision < 1 ||
            !WorkspaceTerminationFenceRules.IsSha256(
                policyEvidenceSha256) ||
            actor.Length == 0 ||
            completedAtUtc == default)
        {
            return Result.Failure<WorkspaceTerminationFenceReceipt>(
                WorkspaceTerminationFenceErrors.ReceiptCoordinatesInvalid);
        }

        if (!IsValidTransition(
                action,
                selectedFenceVersion,
                resultingFenceVersion,
                resultingState))
        {
            return Result.Failure<WorkspaceTerminationFenceReceipt>(
                WorkspaceTerminationFenceErrors.ReceiptTransitionInvalid);
        }

        return Result.Success(new WorkspaceTerminationFenceReceipt(id, scopeId)
        {
            FenceId = fenceId,
            ProcessId = processId,
            CaseId = caseId,
            ApprovalRevision = approvalRevision,
            OperationRevision = operationRevision,
            WorkItemId = workItemId,
            IdempotencyKey = idempotencyKey,
            TerminationEpoch = terminationEpoch,
            Action = action,
            SelectedFenceVersion = selectedFenceVersion,
            ResultingFenceVersion = resultingFenceVersion,
            ResultingState = resultingState,
            PolicyEvidenceSha256 = policyEvidenceSha256,
            ActorId = actor,
            CompletedAtUtc = completedAtUtc
        });
    }

    private static bool IsValidTransition(
        WorkspaceTerminationFenceAction action,
        long selectedVersion,
        long resultingVersion,
        WorkspaceTerminationFenceState resultingState) =>
        action switch
        {
            WorkspaceTerminationFenceAction.Freeze =>
                selectedVersion == 0 &&
                resultingVersion == 1 &&
                resultingState == WorkspaceTerminationFenceState.Frozen,
            WorkspaceTerminationFenceAction.BeginDestruction =>
                selectedVersion >= 1 &&
                resultingVersion == selectedVersion + 1 &&
                resultingState ==
                    WorkspaceTerminationFenceState.DestructionStarted,
            WorkspaceTerminationFenceAction.Close =>
                selectedVersion >= 2 &&
                resultingVersion == selectedVersion + 1 &&
                resultingState == WorkspaceTerminationFenceState.Closed,
            WorkspaceTerminationFenceAction.Release =>
                selectedVersion >= 1 &&
                resultingVersion == selectedVersion + 1 &&
                resultingState == WorkspaceTerminationFenceState.Released,
            _ => false
        };
}
