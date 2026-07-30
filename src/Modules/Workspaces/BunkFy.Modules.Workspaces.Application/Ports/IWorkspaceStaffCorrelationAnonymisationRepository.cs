namespace BunkFy.Modules.Workspaces.Application.Ports;

using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Results;

public interface IWorkspaceStaffCorrelationAnonymisationRepository
{
    Task<WorkspaceStaffCorrelationAnonymisationSnapshot> ResolveAsync(
        string tenantId,
        Guid staffMemberId,
        long selectedStaffVersion,
        string? subjectId,
        CancellationToken cancellationToken);

    Task<WorkspaceStaffCorrelationAnonymisationSnapshot> ReadAsync(
        string tenantId,
        Guid anchorProcessId,
        long selectedAnchorVersion,
        CancellationToken cancellationToken);

    Task<WorkspaceStaffCorrelationAnonymisationSnapshot>
        ReadAnonymisedAsync(
            string tenantId,
            Guid anchorProcessId,
            long resultingAnchorVersion,
            Guid ownerReceiptId,
            CancellationToken cancellationToken);

    Task<WorkspaceStaffCorrelationAnonymisationReceipt?>
        FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken);

    Task<WorkspaceStaffCorrelationAnonymisationTombstone?>
        GetTombstoneAsync(
            Guid anchorProcessId,
            CancellationToken cancellationToken);

    Task<WorkspaceStaffCorrelationAnonymisationTombstone?>
        FindTombstoneAsync(
            string tenantId,
            Guid staffMemberId,
            long selectedStaffVersion,
            CancellationToken cancellationToken);

    Task<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt?>
        GetRestoreReceiptAsync(
            Guid ledgerEntryId,
            CancellationToken cancellationToken);

    Task<Result<WorkspaceStaffCorrelationAnonymisationReceipt>>
        ApplyAsync(
            WorkspaceStaffCorrelationAnonymisationApplyRequest
                request,
            CancellationToken cancellationToken);

    Task<Result<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt>>
        RestoreAsync(
            WorkspaceStaffCorrelationAnonymisationRestoreRequest
                request,
            CancellationToken cancellationToken);
}

public sealed record
    WorkspaceStaffCorrelationAnonymisationApplyRequest(
        Guid ReceiptId,
        string TenantId,
        Guid IdempotencyKey,
        Guid CaseId,
        long ApprovalRevision,
        long OperationRevision,
        WorkspaceStaffCorrelationAnonymisationSnapshot Snapshot,
        string ApprovalEvidenceSha256,
        string StateBindingSha256,
        string ActorId,
        DateTimeOffset CompletedAtUtc);

public sealed record
    WorkspaceStaffCorrelationAnonymisationRestoreRequest(
        string TenantId,
        Guid LedgerEntryId,
        long TenantSequence,
        string LedgerEntrySha256,
        Guid AnchorProcessId,
        int OwnerReceiptContractVersion,
        Guid OwnerReceiptId,
        string OwnerReceiptSha256,
        long ResultingAnchorVersion,
        DateTimeOffset OriginallyCompletedAtUtc,
        DateTimeOffset ReplayedAtUtc);

public sealed record WorkspaceStaffCorrelationAnonymisationSnapshot(
    WorkspaceStaffCorrelationAnonymisationSnapshotStatus Status,
    Guid? AnchorProcessId,
    long? AnchorProcessVersion,
    Guid? StaffMemberId,
    long? SelectedStaffVersion,
    string? SubjectId,
    string? StateSha256,
    int OnboardingRecordCount,
    int AccessProcessRecordCount,
    int AccessPlanRecordCount)
{
    public static WorkspaceStaffCorrelationAnonymisationSnapshot
        NoCorrelation() =>
        Empty(
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .NoCorrelation);

    public static WorkspaceStaffCorrelationAnonymisationSnapshot
        Unavailable() =>
        Empty(
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .Unavailable);

    public static WorkspaceStaffCorrelationAnonymisationSnapshot
        Stale() =>
        Empty(
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .Stale);

    public static WorkspaceStaffCorrelationAnonymisationSnapshot
        ActiveOnboarding() =>
        Empty(
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .ActiveOnboarding);

    public static WorkspaceStaffCorrelationAnonymisationSnapshot
        ActiveAccessProcess() =>
        Empty(
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .ActiveAccessProcess);

    public static WorkspaceStaffCorrelationAnonymisationSnapshot
        Conflict() =>
        Empty(
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .Conflict);

    public static WorkspaceStaffCorrelationAnonymisationSnapshot
        Oversized() =>
        Empty(
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .Oversized);

    private static WorkspaceStaffCorrelationAnonymisationSnapshot Empty(
        WorkspaceStaffCorrelationAnonymisationSnapshotStatus status) =>
        new(
            status,
            AnchorProcessId: null,
            AnchorProcessVersion: null,
            StaffMemberId: null,
            SelectedStaffVersion: null,
            SubjectId: null,
            StateSha256: null,
            OnboardingRecordCount: 0,
            AccessProcessRecordCount: 0,
            AccessPlanRecordCount: 0);
}

public enum WorkspaceStaffCorrelationAnonymisationSnapshotStatus
{
    Unknown = 0,
    Eligible = 1,
    NoCorrelation = 2,
    Unavailable = 3,
    Stale = 4,
    ActiveOnboarding = 5,
    ActiveAccessProcess = 6,
    Conflict = 7,
    Oversized = 8
}
