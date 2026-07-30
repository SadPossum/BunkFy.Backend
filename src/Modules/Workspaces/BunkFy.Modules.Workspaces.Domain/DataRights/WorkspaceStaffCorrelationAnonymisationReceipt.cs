namespace BunkFy.Modules.Workspaces.Domain.DataRights;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceStaffCorrelationAnonymisationReceipt
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;
    public const int Sha256Length = 64;
    public const string PseudonymPrefix = "anonymised:";

    private WorkspaceStaffCorrelationAnonymisationReceipt() { }

    private WorkspaceStaffCorrelationAnonymisationReceipt(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid IdempotencyKey { get; private set; }
    public Guid CaseId { get; private set; }
    public long ApprovalRevision { get; private set; }
    public long OperationRevision { get; private set; }
    public Guid AnchorProcessId { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public long SelectedStaffVersion { get; private set; }
    public long SelectedAnchorVersion { get; private set; }
    public long ResultingAnchorVersion { get; private set; }
    public int OnboardingRecordsScrubbed { get; private set; }
    public int AccessProcessRecordsScrubbed { get; private set; }
    public int AccessPlanRecordsScrubbed { get; private set; }
    public WorkspaceStaffCorrelationAnonymisationDisposition Disposition
    {
        get;
        private set;
    }
    public WorkspaceStaffCorrelationAnonymisationReason Reason
    {
        get;
        private set;
    }
    public string ApprovalEvidenceSha256 { get; private set; } =
        string.Empty;
    public string StateBindingSha256 { get; private set; } =
        string.Empty;
    public string ResultingStateSha256 { get; private set; } =
        string.Empty;
    public string ActorId { get; private set; } = string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<WorkspaceStaffCorrelationAnonymisationReceipt>
        Create(
            Guid receiptId,
            string tenantId,
            Guid idempotencyKey,
            Guid caseId,
            long approvalRevision,
            long operationRevision,
            Guid anchorProcessId,
            Guid staffMemberId,
            long selectedStaffVersion,
            long selectedAnchorVersion,
            long resultingAnchorVersion,
            int onboardingRecordsScrubbed,
            int accessProcessRecordsScrubbed,
            int accessPlanRecordsScrubbed,
            string approvalEvidenceSha256,
            string stateBindingSha256,
            string resultingStateSha256,
            string actorId,
            DateTimeOffset completedAtUtc)
    {
        string actor = actorId?.Trim() ?? string.Empty;
        string approvalDigest = NormalizeSha256(
            approvalEvidenceSha256);
        string bindingDigest = NormalizeSha256(
            stateBindingSha256);
        string resultingDigest = NormalizeSha256(
            resultingStateSha256);
        if (receiptId == Guid.Empty ||
            idempotencyKey == Guid.Empty ||
            caseId == Guid.Empty ||
            anchorProcessId == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            approvalRevision <= 0 ||
            operationRevision <= approvalRevision ||
            selectedStaffVersion <= 0 ||
            selectedAnchorVersion <= 0 ||
            resultingAnchorVersion != selectedAnchorVersion + 1 ||
            onboardingRecordsScrubbed < 0 ||
            accessProcessRecordsScrubbed <= 0 ||
            accessPlanRecordsScrubbed < 0 ||
            actor.Length is 0 or >
                WorkspaceStaffAccessProcess.ActorIdMaxLength ||
            completedAtUtc == default ||
            !IsSha256(approvalDigest) ||
            !IsSha256(bindingDigest) ||
            !IsSha256(resultingDigest) ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        WorkspaceStaffCorrelationAnonymisationReceipt receipt =
            new(receiptId, scopeId)
            {
                ContractVersion = CurrentContractVersion,
                IdempotencyKey = idempotencyKey,
                CaseId = caseId,
                ApprovalRevision = approvalRevision,
                OperationRevision = operationRevision,
                AnchorProcessId = anchorProcessId,
                StaffMemberId = staffMemberId,
                SelectedStaffVersion = selectedStaffVersion,
                SelectedAnchorVersion = selectedAnchorVersion,
                ResultingAnchorVersion = resultingAnchorVersion,
                OnboardingRecordsScrubbed =
                    onboardingRecordsScrubbed,
                AccessProcessRecordsScrubbed =
                    accessProcessRecordsScrubbed,
                AccessPlanRecordsScrubbed =
                    accessPlanRecordsScrubbed,
                Disposition =
                    WorkspaceStaffCorrelationAnonymisationDisposition
                        .Completed,
                Reason =
                    WorkspaceStaffCorrelationAnonymisationReason
                        .SubjectCorrelationsPseudonymised,
                ApprovalEvidenceSha256 = approvalDigest,
                StateBindingSha256 = bindingDigest,
                ResultingStateSha256 = resultingDigest,
                ActorId = actor,
                CompletedAtUtc =
                    completedAtUtc.ToUniversalTime()
            };
        receipt.CanonicalSha256 =
            receipt.ComputeCanonicalSha256();
        return Result.Success(receipt);
    }

    public bool Matches(
        Guid idempotencyKey,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid anchorProcessId,
        long selectedAnchorVersion,
        string approvalEvidenceSha256,
        string actorId) =>
        this.HasValidCanonicalProof() &&
        this.IdempotencyKey == idempotencyKey &&
        this.CaseId == caseId &&
        this.ApprovalRevision == approvalRevision &&
        this.OperationRevision == operationRevision &&
        this.AnchorProcessId == anchorProcessId &&
        this.SelectedAnchorVersion == selectedAnchorVersion &&
        string.Equals(
            this.ApprovalEvidenceSha256,
            NormalizeSha256(approvalEvidenceSha256),
            StringComparison.Ordinal) &&
        string.Equals(
            this.ActorId,
            actorId?.Trim(),
            StringComparison.Ordinal);

    public bool HasValidCanonicalProof() =>
        this.ContractVersion == CurrentContractVersion &&
        this.Disposition ==
            WorkspaceStaffCorrelationAnonymisationDisposition.Completed &&
        this.Reason ==
            WorkspaceStaffCorrelationAnonymisationReason
                .SubjectCorrelationsPseudonymised &&
        this.ResultingAnchorVersion ==
            this.SelectedAnchorVersion + 1 &&
        this.AccessProcessRecordsScrubbed > 0 &&
        IsSha256(this.ApprovalEvidenceSha256) &&
        IsSha256(this.StateBindingSha256) &&
        IsSha256(this.ResultingStateSha256) &&
        IsSha256(this.CanonicalSha256) &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    public string CreateSubjectPseudonym() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{PseudonymPrefix}{this.Id:N}");

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(canonical, this.ContractVersion);
        Append(canonical, this.Id);
        Append(canonical, this.ScopeId);
        Append(canonical, this.IdempotencyKey);
        Append(canonical, this.CaseId);
        Append(canonical, this.ApprovalRevision);
        Append(canonical, this.OperationRevision);
        Append(canonical, this.AnchorProcessId);
        Append(canonical, this.StaffMemberId);
        Append(canonical, this.SelectedStaffVersion);
        Append(canonical, this.SelectedAnchorVersion);
        Append(canonical, this.ResultingAnchorVersion);
        Append(canonical, this.OnboardingRecordsScrubbed);
        Append(canonical, this.AccessProcessRecordsScrubbed);
        Append(canonical, this.AccessPlanRecordsScrubbed);
        Append(canonical, (int)this.Disposition);
        Append(canonical, (int)this.Reason);
        Append(canonical, this.ApprovalEvidenceSha256);
        Append(canonical, this.StateBindingSha256);
        Append(canonical, this.ResultingStateSha256);
        Append(canonical, this.ActorId);
        Append(canonical, this.CompletedAtUtc);
        return Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void Append(
        StringBuilder target,
        object value)
    {
        string text = value switch
        {
            DateTimeOffset timestamp =>
                timestamp.ToUniversalTime().ToString(
                    "O",
                    CultureInfo.InvariantCulture),
            Guid id => id.ToString("N"),
            IFormattable formattable =>
                formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
        target.Append(
            text.Length.ToString(
                CultureInfo.InvariantCulture));
        target.Append(':');
        target.Append(text);
    }

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length == Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<
        WorkspaceStaffCorrelationAnonymisationReceipt> Invalid() =>
        Result.Failure<
            WorkspaceStaffCorrelationAnonymisationReceipt>(
            WorkspaceStaffCorrelationAnonymisationErrors
                .ReceiptInvalid);
}

public enum WorkspaceStaffCorrelationAnonymisationDisposition
{
    Unknown = 0,
    Completed = 1
}

public enum WorkspaceStaffCorrelationAnonymisationReason
{
    Unknown = 0,
    SubjectCorrelationsPseudonymised = 1
}
