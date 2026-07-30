namespace BunkFy.Modules.Staff.Domain.DataRights;

using BunkFy.Modules.Staff.Domain.Errors;
using BunkFy.Modules.Staff.Domain.Models;
using BunkFy.Modules.Staff.Domain.Retention;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class StaffAnonymisationTombstone
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 3;

    private StaffAnonymisationTombstone() { }

    private StaffAnonymisationTombstone(
        Guid staffMemberId,
        string scopeId)
        : base(staffMemberId, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public StaffAnonymisationTombstoneState State { get; private set; }
    public StaffAnonymisationAuthority Authority { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } =
        string.Empty;
    public DateTimeOffset? LastReplayedAtUtc { get; private set; }

    public static Result<StaffAnonymisationTombstone> Create(
        string tenantId,
        Guid staffMemberId,
        DateTimeOffset completedAtUtc,
        string ownerReceiptSha256)
    {
        string receiptDigest =
            ownerReceiptSha256?.Trim().ToLowerInvariant() ??
            string.Empty;
        if (staffMemberId == Guid.Empty ||
            completedAtUtc == default ||
            !IsSha256(receiptDigest) ||
            !TenantIds.TryNormalize(tenantId, out string? scopeId))
        {
            return Invalid();
        }

        return Result.Success(
            new StaffAnonymisationTombstone(
                staffMemberId,
                scopeId)
            {
                ContractVersion = CurrentContractVersion,
                Revision = 1,
                State = StaffAnonymisationTombstoneState.Anonymised,
                Authority = StaffAnonymisationAuthority.DataRights,
                CompletedAtUtc = completedAtUtc.ToUniversalTime(),
                OwnerReceiptSha256 = receiptDigest
            });
    }

    public static Result<StaffAnonymisationTombstone> Restore(
        string tenantId,
        Guid staffMemberId,
        DateTimeOffset originallyCompletedAtUtc,
        string ownerReceiptSha256,
        Guid ledgerEntryId,
        DateTimeOffset replayedAtUtc)
    {
        Result<StaffAnonymisationTombstone> created = Create(
            tenantId,
            staffMemberId,
            originallyCompletedAtUtc.ToUniversalTime(),
            ownerReceiptSha256);
        if (created.IsFailure)
        {
            return created;
        }

        DateTimeOffset timestamp = replayedAtUtc.ToUniversalTime();
        if (ledgerEntryId == Guid.Empty ||
            timestamp == default ||
            timestamp <
                originallyCompletedAtUtc.ToUniversalTime())
        {
            return Invalid();
        }

        created.Value.LedgerEntryId = ledgerEntryId;
        created.Value.LastReplayedAtUtc = timestamp;
        return created;
    }

    public static Result<StaffAnonymisationTombstone> CreateForRetention(
        StaffRetentionAnonymisationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (receipt.ContractVersion !=
                StaffRetentionAnonymisationReceipt
                    .CurrentContractVersion ||
            receipt.StaffMemberId == Guid.Empty ||
            receipt.CompletedAtUtc == default ||
            !receipt.HasValidCanonicalProof())
        {
            return Invalid();
        }

        return Result.Success(
            new StaffAnonymisationTombstone(
                receipt.StaffMemberId,
                receipt.ScopeId)
            {
                ContractVersion = CurrentContractVersion,
                Revision = 1,
                State =
                    StaffAnonymisationTombstoneState.Anonymised,
                Authority = StaffAnonymisationAuthority.Retention,
                CompletedAtUtc =
                    receipt.CompletedAtUtc.ToUniversalTime(),
                OwnerReceiptSha256 = receipt.CanonicalSha256
            });
    }

    public Result AttachRestoreProof(
        Guid ledgerEntryId,
        DateTimeOffset originallyCompletedAtUtc,
        string ownerReceiptSha256,
        DateTimeOffset replayedAtUtc)
    {
        string receiptDigest =
            ownerReceiptSha256?.Trim().ToLowerInvariant() ??
            string.Empty;
        DateTimeOffset completedAtUtc =
            originallyCompletedAtUtc.ToUniversalTime();
        DateTimeOffset timestamp = replayedAtUtc.ToUniversalTime();
        if (ledgerEntryId == Guid.Empty ||
            timestamp == default ||
            timestamp < completedAtUtc ||
            this.Authority != StaffAnonymisationAuthority.DataRights ||
            this.CompletedAtUtc != completedAtUtc ||
            !string.Equals(
                this.OwnerReceiptSha256,
                receiptDigest,
                StringComparison.Ordinal))
        {
            return Result.Failure(
                StaffDomainErrors.AnonymisationTombstoneInvalid);
        }

        if (this.LedgerEntryId.HasValue)
        {
            return this.LedgerEntryId == ledgerEntryId &&
                this.LastReplayedAtUtc.HasValue
                ? Result.Success()
                : Result.Failure(
                    StaffDomainErrors.AnonymisationTombstoneInvalid);
        }

        this.LedgerEntryId = ledgerEntryId;
        this.LastReplayedAtUtc = timestamp;
        this.Revision++;
        return Result.Success();
    }

    public bool MatchesRestore(
        Guid staffMemberId,
        Guid ledgerEntryId,
        DateTimeOffset originallyCompletedAtUtc,
        string ownerReceiptSha256) =>
        this.ContractVersion == CurrentContractVersion &&
        this.Revision >= 1 &&
        this.State == StaffAnonymisationTombstoneState.Anonymised &&
        this.Authority == StaffAnonymisationAuthority.DataRights &&
        this.Id == staffMemberId &&
        this.LedgerEntryId == ledgerEntryId &&
        this.CompletedAtUtc ==
            originallyCompletedAtUtc.ToUniversalTime() &&
        string.Equals(
            this.OwnerReceiptSha256,
            ownerReceiptSha256?.Trim().ToLowerInvariant(),
            StringComparison.Ordinal) &&
        this.LastReplayedAtUtc.HasValue;

    public bool Matches(StaffAnonymisationReceipt receipt) =>
        receipt is not null &&
        this.ContractVersion == CurrentContractVersion &&
        this.Revision >= 1 &&
        this.State == StaffAnonymisationTombstoneState.Anonymised &&
        this.Authority == StaffAnonymisationAuthority.DataRights &&
        this.Id == receipt.StaffMemberId &&
        this.CompletedAtUtc == receipt.CompletedAtUtc &&
        string.Equals(
            this.OwnerReceiptSha256,
            receipt.CanonicalSha256,
            StringComparison.Ordinal);

    public bool MatchesRetention(
        StaffRetentionAnonymisationReceipt receipt) =>
        receipt is not null &&
        receipt.HasValidCanonicalProof() &&
        this.ContractVersion == CurrentContractVersion &&
        this.Revision >= 1 &&
        this.State ==
            StaffAnonymisationTombstoneState.Anonymised &&
        this.Authority == StaffAnonymisationAuthority.Retention &&
        this.Id == receipt.StaffMemberId &&
        this.CompletedAtUtc == receipt.CompletedAtUtc &&
        this.LedgerEntryId is null &&
        this.LastReplayedAtUtc is null &&
        string.Equals(
            this.OwnerReceiptSha256,
            receipt.CanonicalSha256,
            StringComparison.Ordinal);

    private static bool IsSha256(string value) =>
        value.Length == StaffAnonymisationReceipt.Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<StaffAnonymisationTombstone> Invalid() =>
        Result.Failure<StaffAnonymisationTombstone>(
            StaffDomainErrors.AnonymisationTombstoneInvalid);
}
