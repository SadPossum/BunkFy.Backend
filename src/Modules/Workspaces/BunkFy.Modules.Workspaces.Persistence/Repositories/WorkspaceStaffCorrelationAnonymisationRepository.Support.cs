namespace BunkFy.Modules.Workspaces.Persistence.Repositories;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Results;

internal sealed partial class
    WorkspaceStaffCorrelationAnonymisationRepository
{
    private static bool HasExpectedResult(
        WorkspaceStaffCorrelationAnonymisationSnapshot selected,
        WorkspaceStaffCorrelationAnonymisationSnapshot resulting) =>
        resulting.Status ==
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .Eligible &&
        resulting.AnchorProcessId == selected.AnchorProcessId &&
        resulting.AnchorProcessVersion ==
            selected.AnchorProcessVersion + 1 &&
        resulting.StaffMemberId == selected.StaffMemberId &&
        resulting.SelectedStaffVersion ==
            selected.SelectedStaffVersion &&
        resulting.OnboardingRecordCount ==
            selected.OnboardingRecordCount &&
        resulting.AccessProcessRecordCount ==
            selected.AccessProcessRecordCount &&
        resulting.AccessPlanRecordCount ==
            selected.AccessPlanRecordCount &&
        IsSha256(resulting.StateSha256);

    private static bool MatchesTombstone(
        WorkspaceStaffCorrelationAnonymisationSnapshot snapshot,
        WorkspaceStaffCorrelationAnonymisationTombstone
            tombstone) =>
        snapshot.Status ==
            WorkspaceStaffCorrelationAnonymisationSnapshotStatus
                .Eligible &&
        snapshot.AnchorProcessId == tombstone.Id &&
        snapshot.AnchorProcessVersion ==
            tombstone.ResultingAnchorVersion &&
        snapshot.StaffMemberId == tombstone.StaffMemberId &&
        snapshot.SelectedStaffVersion ==
            tombstone.SelectedStaffVersion &&
        string.Equals(
            snapshot.StateSha256,
            tombstone.ResultingStateSha256,
            StringComparison.Ordinal);

    private static bool HasValidApplyRequest(
        WorkspaceStaffCorrelationAnonymisationApplyRequest request) =>
        request is not null &&
        request.ReceiptId != Guid.Empty &&
        request.IdempotencyKey != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        request.OperationRevision > request.ApprovalRevision &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        IsSha256(request.ApprovalEvidenceSha256) &&
        IsSha256(request.StateBindingSha256) &&
        !string.IsNullOrWhiteSpace(request.ActorId) &&
        request.CompletedAtUtc != default;

    private static bool HasValidRestoreRequest(
        WorkspaceStaffCorrelationAnonymisationRestoreRequest request) =>
        request is not null &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        request.LedgerEntryId != Guid.Empty &&
        request.TenantSequence > 0 &&
        IsSha256(request.LedgerEntrySha256) &&
        request.AnchorProcessId != Guid.Empty &&
        request.OwnerReceiptContractVersion > 0 &&
        request.OwnerReceiptId != Guid.Empty &&
        IsSha256(request.OwnerReceiptSha256) &&
        request.ResultingAnchorVersion > 1 &&
        request.OriginallyCompletedAtUtc != default &&
        request.ReplayedAtUtc != default &&
        request.ReplayedAtUtc >=
            request.OriginallyCompletedAtUtc;

    private static string ComputeDigest(
        AnchorState anchor,
        IReadOnlyCollection<OnboardingState> onboarding,
        IReadOnlyCollection<AccessProcessState> accessProcesses,
        IReadOnlyCollection<AccessPlanState> accessPlans)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, anchor.Id);
        Append(hash, anchor.Version);
        Append(hash, anchor.StaffMemberId);
        Append(hash, anchor.TargetStaffVersion);
        Append(hash, anchor.SubjectId);
        foreach (OnboardingState record in onboarding)
        {
            Append(hash, "onboarding");
            Append(hash, record.Id);
            Append(hash, record.Version);
            Append(hash, (int)record.Status);
            Append(hash, record.StaffMemberId);
            Append(hash, record.SubjectId);
        }

        foreach (AccessProcessState record in accessProcesses)
        {
            Append(hash, "access-process");
            Append(hash, record.Id);
            Append(hash, record.Version);
            Append(hash, record.StaffMemberId);
            Append(hash, record.TargetStaffVersion);
            Append(hash, (int)record.TargetState);
            Append(hash, (int)record.State);
            Append(hash, record.SubjectId);
            Append(hash, record.RequestedBy);
        }

        foreach (AccessPlanState record in accessPlans)
        {
            Append(hash, "access-plan");
            Append(hash, record.Id);
            Append(hash, record.Version);
            Append(hash, (int)record.Status);
            Append(hash, record.CreatedBySubjectId);
        }

        byte[] digest = hash.GetHashAndReset();
        try
        {
            return Convert.ToHexStringLower(digest);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static void Append(
        IncrementalHash hash,
        object? value)
    {
        string text = value switch
        {
            null => "-",
            Guid id => id.ToString("N"),
            IFormattable formattable =>
                formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "-"
        };
        byte[] bytes = Encoding.UTF8.GetBytes(
            string.Create(
                CultureInfo.InvariantCulture,
                $"{text.Length}:{text}"));
        try
        {
            hash.AppendData(bytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string? NormalizeSubject(string? subjectId)
    {
        string normalized = subjectId?.Trim() ?? string.Empty;
        return normalized.Length == 0 ? null : normalized;
    }

    private static string CreatePseudonym(Guid receiptId) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{DataRightsPseudonymPrefix}{receiptId:N}");

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsPseudonym(string subjectId) =>
        subjectId.StartsWith(
            WorkspaceStaffRetentionCorrelationReceipt
                .PseudonymPrefix,
            StringComparison.OrdinalIgnoreCase) ||
        subjectId.StartsWith(
            DataRightsPseudonymPrefix,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsSha256(string? value) =>
        value is
        {
            Length:
                WorkspaceStaffCorrelationAnonymisationReceipt
                    .Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static bool IsActive(
        WorkspaceStaffOnboardingState status) =>
        status is not (
            WorkspaceStaffOnboardingState.Completed or
            WorkspaceStaffOnboardingState.Rejected or
            WorkspaceStaffOnboardingState.Superseded or
            WorkspaceStaffOnboardingState.Expired);

    private static Result<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
        RestoreConflict() =>
        Result.Failure<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>(
            WorkspaceStaffCorrelationAnonymisationApplicationErrors
                .RestoreProofConflict);

    private sealed record MutationCounts(
        int Onboarding,
        int AccessProcesses,
        int AccessPlans)
    {
        public bool Matches(
            WorkspaceStaffCorrelationAnonymisationSnapshot snapshot) =>
            this.Onboarding == snapshot.OnboardingRecordCount &&
            this.AccessProcesses ==
                snapshot.AccessProcessRecordCount &&
            this.AccessPlans == snapshot.AccessPlanRecordCount;
    }

    private sealed record AnchorState(
        Guid Id,
        long Version,
        Guid StaffMemberId,
        long TargetStaffVersion,
        string SubjectId);

    private sealed record OnboardingState(
        Guid Id,
        long Version,
        WorkspaceStaffOnboardingState Status,
        Guid? StaffMemberId,
        string SubjectId);

    private sealed record AccessProcessState(
        Guid Id,
        long Version,
        Guid StaffMemberId,
        long TargetStaffVersion,
        WorkspaceStaffAccessTargetState TargetState,
        WorkspaceStaffAccessProcessState State,
        string SubjectId,
        string RequestedBy);

    private sealed record AccessPlanState(
        Guid Id,
        long Version,
        WorkspaceStaffAccessPlanState Status,
        string CreatedBySubjectId);
}
