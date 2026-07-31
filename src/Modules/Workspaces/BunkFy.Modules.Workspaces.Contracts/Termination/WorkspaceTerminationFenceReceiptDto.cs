namespace BunkFy.Modules.Workspaces.Contracts;

public sealed record WorkspaceTerminationFenceReceiptDto(
    Guid ReceiptId,
    Guid FenceId,
    Guid ProcessId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    Guid WorkItemId,
    Guid IdempotencyKey,
    Guid TerminationEpoch,
    WorkspaceTerminationFenceActionDto Action,
    long SelectedFenceVersion,
    long ResultingFenceVersion,
    WorkspaceTerminationFenceReceiptStateDto ResultingState,
    string PolicyEvidenceSha256,
    string ActorId,
    DateTimeOffset CompletedAtUtc);

public enum WorkspaceTerminationFenceActionDto
{
    Unknown = 0,
    Freeze = 1,
    BeginDestruction = 2,
    Close = 3,
    Release = 4
}

public enum WorkspaceTerminationFenceReceiptStateDto
{
    Unknown = 0,
    Frozen = 1,
    DestructionStarted = 2,
    Closed = 3,
    Released = 4
}
