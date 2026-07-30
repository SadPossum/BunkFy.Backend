namespace BunkFy.Modules.Workspaces.Domain.DataRights;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class
    WorkspaceStaffCorrelationAnonymisationRestoreReceipt
    : ScopedEntity<Guid>
{
    public const int CurrentContractVersion = 1;

    private WorkspaceStaffCorrelationAnonymisationRestoreReceipt() { }

    private WorkspaceStaffCorrelationAnonymisationRestoreReceipt(
        Guid id,
        string scopeId)
        : base(id, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public Guid LedgerEntryId { get; private set; }
    public long TenantSequence { get; private set; }
    public string LedgerEntrySha256 { get; private set; } =
        string.Empty;
    public Guid AnchorProcessId { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public int OwnerReceiptContractVersion { get; private set; }
    public Guid OwnerReceiptId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } =
        string.Empty;
    public long ResultingAnchorVersion { get; private set; }
    public string ResultingStateSha256 { get; private set; } =
        string.Empty;
    public int OnboardingRecordsScrubbed { get; private set; }
    public int AccessProcessRecordsScrubbed { get; private set; }
    public int AccessPlanRecordsScrubbed { get; private set; }
    public DateTimeOffset OriginallyCompletedAtUtc
    {
        get;
        private set;
    }
    public long TombstoneRevision { get; private set; }
    public DateTimeOffset ReplayedAtUtc { get; private set; }
    public string CanonicalSha256 { get; private set; } = string.Empty;

    public static Result<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt> Create(
            string tenantId,
            Guid ledgerEntryId,
            long tenantSequence,
            string ledgerEntrySha256,
            Guid anchorProcessId,
            Guid staffMemberId,
            int ownerReceiptContractVersion,
            Guid ownerReceiptId,
            string ownerReceiptSha256,
            long resultingAnchorVersion,
            string resultingStateSha256,
            int onboardingRecordsScrubbed,
            int accessProcessRecordsScrubbed,
            int accessPlanRecordsScrubbed,
            DateTimeOffset originallyCompletedAtUtc,
            long tombstoneRevision,
            DateTimeOffset replayedAtUtc)
    {
        string ledgerDigest =
            NormalizeSha256(ledgerEntrySha256);
        string receiptDigest =
            NormalizeSha256(ownerReceiptSha256);
        string stateDigest =
            NormalizeSha256(resultingStateSha256);
        DateTimeOffset completed =
            originallyCompletedAtUtc.ToUniversalTime();
        DateTimeOffset replayed =
            replayedAtUtc.ToUniversalTime();
        if (!TenantIds.TryNormalize(
                tenantId,
                out string? scopeId) ||
            ledgerEntryId == Guid.Empty ||
            tenantSequence <= 0 ||
            !IsSha256(ledgerDigest) ||
            anchorProcessId == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            ownerReceiptContractVersion <= 0 ||
            ownerReceiptId == Guid.Empty ||
            !IsSha256(receiptDigest) ||
            resultingAnchorVersion <= 1 ||
            !IsSha256(stateDigest) ||
            onboardingRecordsScrubbed < 0 ||
            accessProcessRecordsScrubbed <= 0 ||
            accessPlanRecordsScrubbed < 0 ||
            completed == default ||
            tombstoneRevision <= 0 ||
            replayed == default ||
            replayed < completed)
        {
            return Invalid();
        }

        WorkspaceStaffCorrelationAnonymisationRestoreReceipt
            receipt = new(ledgerEntryId, scopeId)
            {
                ContractVersion = CurrentContractVersion,
                LedgerEntryId = ledgerEntryId,
                TenantSequence = tenantSequence,
                LedgerEntrySha256 = ledgerDigest,
                AnchorProcessId = anchorProcessId,
                StaffMemberId = staffMemberId,
                OwnerReceiptContractVersion =
                    ownerReceiptContractVersion,
                OwnerReceiptId = ownerReceiptId,
                OwnerReceiptSha256 = receiptDigest,
                ResultingAnchorVersion =
                    resultingAnchorVersion,
                ResultingStateSha256 = stateDigest,
                OnboardingRecordsScrubbed =
                    onboardingRecordsScrubbed,
                AccessProcessRecordsScrubbed =
                    accessProcessRecordsScrubbed,
                AccessPlanRecordsScrubbed =
                    accessPlanRecordsScrubbed,
                OriginallyCompletedAtUtc = completed,
                TombstoneRevision = tombstoneRevision,
                ReplayedAtUtc = replayed
            };
        receipt.CanonicalSha256 =
            receipt.ComputeCanonicalSha256();
        return Result.Success(receipt);
    }

    public bool Matches(
        string tenantId,
        Guid ledgerEntryId,
        long tenantSequence,
        string ledgerEntrySha256,
        Guid anchorProcessId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingAnchorVersion,
        DateTimeOffset originallyCompletedAtUtc) =>
        this.HasValidCanonicalProof() &&
        string.Equals(
            this.ScopeId,
            tenantId,
            StringComparison.Ordinal) &&
        this.Id == ledgerEntryId &&
        this.LedgerEntryId == ledgerEntryId &&
        this.TenantSequence == tenantSequence &&
        string.Equals(
            this.LedgerEntrySha256,
            NormalizeSha256(ledgerEntrySha256),
            StringComparison.Ordinal) &&
        this.AnchorProcessId == anchorProcessId &&
        this.OwnerReceiptContractVersion ==
            ownerReceiptContractVersion &&
        this.OwnerReceiptId == ownerReceiptId &&
        string.Equals(
            this.OwnerReceiptSha256,
            NormalizeSha256(ownerReceiptSha256),
            StringComparison.Ordinal) &&
        this.ResultingAnchorVersion == resultingAnchorVersion &&
        this.OriginallyCompletedAtUtc ==
            originallyCompletedAtUtc.ToUniversalTime();

    public bool HasValidCanonicalProof() =>
        this.ContractVersion == CurrentContractVersion &&
        this.Id == this.LedgerEntryId &&
        this.LedgerEntryId != Guid.Empty &&
        this.TenantSequence > 0 &&
        IsSha256(this.LedgerEntrySha256) &&
        this.AnchorProcessId != Guid.Empty &&
        this.StaffMemberId != Guid.Empty &&
        this.OwnerReceiptContractVersion > 0 &&
        this.OwnerReceiptId != Guid.Empty &&
        IsSha256(this.OwnerReceiptSha256) &&
        this.ResultingAnchorVersion > 1 &&
        IsSha256(this.ResultingStateSha256) &&
        this.AccessProcessRecordsScrubbed > 0 &&
        this.OriginallyCompletedAtUtc != default &&
        this.TombstoneRevision > 0 &&
        this.ReplayedAtUtc >= this.OriginallyCompletedAtUtc &&
        IsSha256(this.CanonicalSha256) &&
        string.Equals(
            this.CanonicalSha256,
            this.ComputeCanonicalSha256(),
            StringComparison.Ordinal);

    private string ComputeCanonicalSha256()
    {
        StringBuilder canonical = new();
        Append(canonical, this.ContractVersion);
        Append(canonical, this.Id);
        Append(canonical, this.ScopeId);
        Append(canonical, this.LedgerEntryId);
        Append(canonical, this.TenantSequence);
        Append(canonical, this.LedgerEntrySha256);
        Append(canonical, this.AnchorProcessId);
        Append(canonical, this.StaffMemberId);
        Append(canonical, this.OwnerReceiptContractVersion);
        Append(canonical, this.OwnerReceiptId);
        Append(canonical, this.OwnerReceiptSha256);
        Append(canonical, this.ResultingAnchorVersion);
        Append(canonical, this.ResultingStateSha256);
        Append(canonical, this.OnboardingRecordsScrubbed);
        Append(canonical, this.AccessProcessRecordsScrubbed);
        Append(canonical, this.AccessPlanRecordsScrubbed);
        Append(canonical, this.OriginallyCompletedAtUtc);
        Append(canonical, this.TombstoneRevision);
        Append(canonical, this.ReplayedAtUtc);
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
        value.Length ==
            WorkspaceStaffCorrelationAnonymisationReceipt.Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt> Invalid() =>
        Result.Failure<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>(
            WorkspaceStaffCorrelationAnonymisationErrors
                .RestoreReceiptInvalid);
}
