namespace BunkFy.Modules.Inventory.Domain.DataRights;

using BunkFy.Modules.Inventory.Domain.Errors;
using Gma.Framework.Domain.Models;
using Gma.Framework.Naming;
using Gma.Framework.Results;

public sealed class InventoryAllocationAnonymisationTombstone
    : ScopedAggregateRoot<Guid>
{
    public const int CurrentContractVersion = 1;

    private InventoryAllocationAnonymisationTombstone() { }

    private InventoryAllocationAnonymisationTombstone(
        Guid allocationId,
        string scopeId)
        : base(allocationId, scopeId)
    {
    }

    public int ContractVersion { get; private set; }
    public long Revision { get; private set; }
    public Guid PropertyId { get; private set; }
    public int OwnerReceiptContractVersion { get; private set; }
    public Guid OwnerReceiptId { get; private set; }
    public string OwnerReceiptSha256 { get; private set; } = string.Empty;
    public long ResultingAllocationVersion { get; private set; }
    public Guid ResultingReservationPseudonym { get; private set; }
    public bool AllocationPresent { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public DateTimeOffset? LastReplayedAtUtc { get; private set; }

    public static Result<InventoryAllocationAnonymisationTombstone> Create(
        InventoryAllocationAnonymisationReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        if (!receipt.MatchesOwnerProof(
                receipt.ContractVersion,
                receipt.Id,
                receipt.PropertyId,
                receipt.AllocationId,
                receipt.ResultingAllocationVersion,
                receipt.CanonicalSha256,
                receipt.CompletedAtUtc))
        {
            return Invalid();
        }

        return Result.Success(
            new InventoryAllocationAnonymisationTombstone(
                receipt.AllocationId,
                receipt.ScopeId)
            {
                ContractVersion = CurrentContractVersion,
                Revision = 1,
                PropertyId = receipt.PropertyId,
                OwnerReceiptContractVersion = receipt.ContractVersion,
                OwnerReceiptId = receipt.Id,
                OwnerReceiptSha256 = receipt.CanonicalSha256,
                ResultingAllocationVersion =
                    receipt.ResultingAllocationVersion,
                ResultingReservationPseudonym =
                    receipt.ResultingReservationPseudonym,
                AllocationPresent = true,
                CompletedAtUtc = receipt.CompletedAtUtc.ToUniversalTime()
            });
    }

    public static Result<InventoryAllocationAnonymisationTombstone> Restore(
        string tenantId,
        Guid allocationId,
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingAllocationVersion,
        Guid resultingReservationPseudonym,
        bool allocationPresent,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId,
        DateTimeOffset replayedAtUtc)
    {
        string receiptSha256 = NormalizeSha256(ownerReceiptSha256);
        DateTimeOffset completedAtUtc =
            originallyCompletedAtUtc.ToUniversalTime();
        DateTimeOffset restoredAtUtc = replayedAtUtc.ToUniversalTime();
        if (!TenantIds.TryNormalize(tenantId, out string? scopeId) ||
            allocationId == Guid.Empty ||
            propertyId == Guid.Empty ||
            ownerReceiptContractVersion <= 0 ||
            ownerReceiptId == Guid.Empty ||
            !IsSha256(receiptSha256) ||
            resultingAllocationVersion <= 0 ||
            resultingReservationPseudonym == Guid.Empty ||
            completedAtUtc == default ||
            ledgerEntryId == Guid.Empty ||
            restoredAtUtc == default ||
            restoredAtUtc < completedAtUtc)
        {
            return Invalid();
        }

        return Result.Success(
            new InventoryAllocationAnonymisationTombstone(
                allocationId,
                scopeId)
            {
                ContractVersion = CurrentContractVersion,
                Revision = 1,
                PropertyId = propertyId,
                OwnerReceiptContractVersion =
                    ownerReceiptContractVersion,
                OwnerReceiptId = ownerReceiptId,
                OwnerReceiptSha256 = receiptSha256,
                ResultingAllocationVersion =
                    resultingAllocationVersion,
                ResultingReservationPseudonym =
                    resultingReservationPseudonym,
                AllocationPresent = allocationPresent,
                CompletedAtUtc = completedAtUtc,
                LedgerEntryId = ledgerEntryId,
                LastReplayedAtUtc = restoredAtUtc
            });
    }

    public Result AttachRestoreProof(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingAllocationVersion,
        Guid resultingReservationPseudonym,
        bool allocationPresent,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId,
        DateTimeOffset replayedAtUtc)
    {
        DateTimeOffset restoredAtUtc = replayedAtUtc.ToUniversalTime();
        if (!this.MatchesOwnerProofCoordinates(
                propertyId,
                ownerReceiptContractVersion,
                ownerReceiptId,
                ownerReceiptSha256,
                resultingAllocationVersion,
                resultingReservationPseudonym,
                originallyCompletedAtUtc) ||
            ledgerEntryId == Guid.Empty ||
            restoredAtUtc == default ||
            restoredAtUtc < this.CompletedAtUtc)
        {
            return Result.Failure(
                InventoryDomainErrors
                    .AllocationAnonymisationTombstoneInvalid);
        }

        if (this.LedgerEntryId.HasValue)
        {
            return this.LedgerEntryId == ledgerEntryId &&
                this.LastReplayedAtUtc.HasValue &&
                this.AllocationPresent == allocationPresent
                ? Result.Success()
                : Result.Failure(
                    InventoryDomainErrors
                        .AllocationAnonymisationTombstoneInvalid);
        }

        this.AllocationPresent = allocationPresent;
        this.LedgerEntryId = ledgerEntryId;
        this.LastReplayedAtUtc = restoredAtUtc;
        this.Revision++;
        return Result.Success();
    }

    public bool Matches(
        InventoryAllocationAnonymisationReceipt receipt) =>
        receipt is not null &&
        this.Id == receipt.AllocationId &&
        this.MatchesOwnerProof(
            receipt.PropertyId,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingAllocationVersion,
            receipt.ResultingReservationPseudonym,
            allocationPresent: true,
            receipt.CompletedAtUtc);

    public bool MatchesRestore(
        Guid allocationId,
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingAllocationVersion,
        Guid resultingReservationPseudonym,
        bool allocationPresent,
        DateTimeOffset originallyCompletedAtUtc,
        Guid ledgerEntryId) =>
        this.Id == allocationId &&
        this.LedgerEntryId == ledgerEntryId &&
        this.LastReplayedAtUtc.HasValue &&
        this.MatchesOwnerProof(
            propertyId,
            ownerReceiptContractVersion,
            ownerReceiptId,
            ownerReceiptSha256,
            resultingAllocationVersion,
            resultingReservationPseudonym,
            allocationPresent,
            originallyCompletedAtUtc);

    private bool MatchesOwnerProof(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingAllocationVersion,
        Guid resultingReservationPseudonym,
        bool allocationPresent,
        DateTimeOffset originallyCompletedAtUtc) =>
        this.MatchesOwnerProofCoordinates(
            propertyId,
            ownerReceiptContractVersion,
            ownerReceiptId,
            ownerReceiptSha256,
            resultingAllocationVersion,
            resultingReservationPseudonym,
            originallyCompletedAtUtc) &&
        this.AllocationPresent == allocationPresent;

    private bool MatchesOwnerProofCoordinates(
        Guid propertyId,
        int ownerReceiptContractVersion,
        Guid ownerReceiptId,
        string ownerReceiptSha256,
        long resultingAllocationVersion,
        Guid resultingReservationPseudonym,
        DateTimeOffset originallyCompletedAtUtc) =>
        this.ContractVersion == CurrentContractVersion &&
        this.Revision >= 1 &&
        this.PropertyId == propertyId &&
        this.OwnerReceiptContractVersion ==
            ownerReceiptContractVersion &&
        this.OwnerReceiptId == ownerReceiptId &&
        string.Equals(
            this.OwnerReceiptSha256,
            NormalizeSha256(ownerReceiptSha256),
            StringComparison.Ordinal) &&
        this.ResultingAllocationVersion ==
            resultingAllocationVersion &&
        this.ResultingReservationPseudonym ==
            resultingReservationPseudonym &&
        this.CompletedAtUtc ==
            originallyCompletedAtUtc.ToUniversalTime();

    private static string NormalizeSha256(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static bool IsSha256(string value) =>
        value.Length ==
            InventoryAllocationAnonymisationReceipt.Sha256Length &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static Result<InventoryAllocationAnonymisationTombstone>
        Invalid() =>
        Result.Failure<InventoryAllocationAnonymisationTombstone>(
            InventoryDomainErrors
                .AllocationAnonymisationTombstoneInvalid);
}
