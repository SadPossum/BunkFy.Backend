namespace BunkFy.Modules.Workspaces.Domain.DataRights;

using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class WorkspaceStaffCorrelationAnonymisationTombstone
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;

    private WorkspaceStaffCorrelationAnonymisationTombstone() { }

    private WorkspaceStaffCorrelationAnonymisationTombstone(
        Guid anchorProcessId,
        string scopeId)
        : base(anchorProcessId, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public Guid StaffMemberId { get; private set; }
    public long SelectedStaffVersion { get; private set; }
    public long SelectedAnchorVersion { get; private set; }
    public long ResultingAnchorVersion { get; private set; }
    public int OwnerReceiptContractVersion { get; private set; }
    public Guid OwnerReceiptId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } =
        string.Empty;
    public string ResultingStateSha256 { get; private set; } =
        string.Empty;
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public DateTimeOffset? LastReplayedAtUtc { get; private set; }

    public static Result<
        WorkspaceStaffCorrelationAnonymisationTombstone> Create(
            WorkspaceStaffCorrelationAnonymisationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!receipt.HasValidCanonicalProof())
        {
            return Invalid();
        }

        return CreateCore(
            receipt.ScopeId,
            receipt.AnchorProcessId,
            receipt.StaffMemberId,
            receipt.SelectedStaffVersion,
            receipt.SelectedAnchorVersion,
            receipt.ResultingAnchorVersion,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingStateSha256,
            receipt.CompletedAtUtc);
    }

    public static Result<
        WorkspaceStaffCorrelationAnonymisationTombstone> Restore(
            string tenantId,
            Guid anchorProcessId,
            Guid staffMemberId,
            long selectedStaffVersion,
            long selectedAnchorVersion,
            long resultingAnchorVersion,
            int ownerReceiptContractVersion,
            Guid ownerReceiptId,
            string ownerReceiptSha256,
            string resultingStateSha256,
            DateTimeOffset originallyCompletedAtUtc,
            Guid ledgerEntryId,
            DateTimeOffset replayedAtUtc)
    {
        Result<WorkspaceStaffCorrelationAnonymisationTombstone>
            created = CreateCore(
                tenantId,
                anchorProcessId,
                staffMemberId,
                selectedStaffVersion,
                selectedAnchorVersion,
                resultingAnchorVersion,
                ownerReceiptContractVersion,
                ownerReceiptId,
                ownerReceiptSha256,
                resultingStateSha256,
                originallyCompletedAtUtc);
        DateTimeOffset replayed =
            replayedAtUtc.ToUniversalTime();
        if (created.IsFailure ||
            ledgerEntryId == Guid.Empty ||
            replayed == default ||
            replayed <
                originallyCompletedAtUtc.ToUniversalTime())
        {
            return Invalid();
        }

        created.Value.LedgerEntryId = ledgerEntryId;
        created.Value.LastReplayedAtUtc = replayed;
        return created;
    }

    public Result AttachRestoreProof(
        Guid ledgerEntryId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingAnchorVersion,
        DateTimeOffset originallyCompletedAtUtc,
        DateTimeOffset replayedAtUtc)
    {
        string receiptDigest =
            NormalizeSha256(ownerReceiptSha256);
        DateTimeOffset completed =
            originallyCompletedAtUtc.ToUniversalTime();
        DateTimeOffset replayed =
            replayedAtUtc.ToUniversalTime();
        if (ledgerEntryId == Guid.Empty ||
            ownerReceiptContractVersion <= 0 ||
            ownerReceiptId == Guid.Empty ||
            resultingAnchorVersion != this.ResultingAnchorVersion ||
            completed != this.CompletedAtUtc ||
            replayed == default ||
            replayed < completed ||
            this.OwnerReceiptContractVersion !=
                ownerReceiptContractVersion ||
            this.OwnerReceiptId != ownerReceiptId ||
            !string.Equals(
                this.OwnerReceiptSha256,
                receiptDigest,
                StringComparison.Ordinal))
        {
            return Result.Failure(
                WorkspaceStaffCorrelationAnonymisationErrors
                    .TombstoneInvalid);
        }

        if (this.LedgerEntryId.HasValue)
        {
            return this.LedgerEntryId == ledgerEntryId &&
                this.LastReplayedAtUtc.HasValue
                ? Result.Success()
                : Result.Failure(
                    WorkspaceStaffCorrelationAnonymisationErrors
                        .TombstoneInvalid);
        }

        this.LedgerEntryId = ledgerEntryId;
        this.LastReplayedAtUtc = replayed;
        this.Revision++;
        return Result.Success();
    }

    public bool Matches(
        WorkspaceStaffCorrelationAnonymisationReceipt receipt) =>
        receipt is not null &&
        receipt.HasValidCanonicalProof() &&
        this.HasValidProof() &&
        this.Id == receipt.AnchorProcessId &&
        this.StaffMemberId == receipt.StaffMemberId &&
        this.SelectedStaffVersion ==
            receipt.SelectedStaffVersion &&
        this.SelectedAnchorVersion ==
            receipt.SelectedAnchorVersion &&
        this.ResultingAnchorVersion ==
            receipt.ResultingAnchorVersion &&
        this.OwnerReceiptContractVersion ==
            receipt.ContractVersion &&
        this.OwnerReceiptId == receipt.Id &&
        string.Equals(
            this.OwnerReceiptSha256,
            receipt.CanonicalSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            this.ResultingStateSha256,
            receipt.ResultingStateSha256,
            StringComparison.Ordinal) &&
        this.CompletedAtUtc == receipt.CompletedAtUtc;

    public bool MatchesRestore(
        Guid ledgerEntryId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingAnchorVersion,
        DateTimeOffset originallyCompletedAtUtc) =>
        this.HasValidProof() &&
        this.LedgerEntryId == ledgerEntryId &&
        this.OwnerReceiptContractVersion ==
            ownerReceiptContractVersion &&
        this.OwnerReceiptId == ownerReceiptId &&
        string.Equals(
            this.OwnerReceiptSha256,
            NormalizeSha256(ownerReceiptSha256),
            StringComparison.Ordinal) &&
        this.ResultingAnchorVersion == resultingAnchorVersion &&
        this.CompletedAtUtc ==
            originallyCompletedAtUtc.ToUniversalTime() &&
        this.LastReplayedAtUtc.HasValue;

    public bool HasValidProof() =>
        this.ContractVersion == CurrentContractVersion &&
        this.Revision > 0 &&
        this.Id != Guid.Empty &&
        this.StaffMemberId != Guid.Empty &&
        this.SelectedStaffVersion > 0 &&
        this.SelectedAnchorVersion > 0 &&
        this.ResultingAnchorVersion ==
            this.SelectedAnchorVersion + 1 &&
        this.OwnerReceiptContractVersion > 0 &&
        this.OwnerReceiptId != Guid.Empty &&
        IsSha256(this.OwnerReceiptSha256) &&
        IsSha256(this.ResultingStateSha256) &&
        this.CompletedAtUtc != default;

    public string CreateSubjectPseudonym() =>
        string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{WorkspaceStaffCorrelationAnonymisationReceipt.PseudonymPrefix}" +
            $"{this.OwnerReceiptId:N}");

    private static Result<
        WorkspaceStaffCorrelationAnonymisationTombstone> CreateCore(
            string tenantId,
            Guid anchorProcessId,
            Guid staffMemberId,
            long selectedStaffVersion,
            long selectedAnchorVersion,
            long resultingAnchorVersion,
            int ownerReceiptContractVersion,
            Guid ownerReceiptId,
            string ownerReceiptSha256,
            string resultingStateSha256,
            DateTimeOffset completedAtUtc)
    {
        string receiptDigest =
            NormalizeSha256(ownerReceiptSha256);
        string stateDigest =
            NormalizeSha256(resultingStateSha256);
        if (anchorProcessId == Guid.Empty ||
            staffMemberId == Guid.Empty ||
            selectedStaffVersion <= 0 ||
            selectedAnchorVersion <= 0 ||
            resultingAnchorVersion != selectedAnchorVersion + 1 ||
            ownerReceiptContractVersion <= 0 ||
            ownerReceiptId == Guid.Empty ||
            !IsSha256(receiptDigest) ||
            !IsSha256(stateDigest) ||
            completedAtUtc == default ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        return Result.Success(
            new WorkspaceStaffCorrelationAnonymisationTombstone(
                anchorProcessId,
                scopeId)
            {
                ContractVersion = CurrentContractVersion,
                Revision = 1,
                StaffMemberId = staffMemberId,
                SelectedStaffVersion = selectedStaffVersion,
                SelectedAnchorVersion = selectedAnchorVersion,
                ResultingAnchorVersion = resultingAnchorVersion,
                OwnerReceiptContractVersion =
                    ownerReceiptContractVersion,
                OwnerReceiptId = ownerReceiptId,
                OwnerReceiptSha256 = receiptDigest,
                ResultingStateSha256 = stateDigest,
                CompletedAtUtc =
                    completedAtUtc.ToUniversalTime()
            });
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
        WorkspaceStaffCorrelationAnonymisationTombstone> Invalid() =>
        Result.Failure<
            WorkspaceStaffCorrelationAnonymisationTombstone>(
            WorkspaceStaffCorrelationAnonymisationErrors
                .TombstoneInvalid);
}
