namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;
using BunkFy.Modules.Inventory.Domain.Errors;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryAllocationAnonymisationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Active_allocation_fails_closed()
    {
        InventoryAllocation allocation = CreateActive();

        Result<InventoryAllocationAnonymisationOutcome> result =
            allocation.Anonymise(
                allocation.Version,
                Guid.NewGuid(),
                Now);

        Assert.True(result.IsFailure);
        Assert.Equal(
            InventoryDomainErrors
                .AllocationNotEligibleForAnonymisation,
            result.Error);
        Assert.False(allocation.IsAnonymised);
    }

    [Fact]
    public void Terminal_allocation_pseudonymises_once_and_blocks_mutation()
    {
        InventoryAllocation allocation = CreateRejected();
        Guid originalReservationId = allocation.ReservationId;
        Guid pseudonym = Guid.NewGuid();

        Result<InventoryAllocationAnonymisationOutcome> result =
            allocation.Anonymise(
                allocation.Version,
                pseudonym,
                Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.SelectedVersion);
        Assert.Equal(2, result.Value.ResultingVersion);
        Assert.Equal(pseudonym, allocation.ReservationId);
        Assert.NotEqual(originalReservationId, allocation.ReservationId);
        Assert.True(allocation.IsAnonymised);
        Assert.Equal(Now, allocation.AnonymisedAtUtc);
        Assert.True(allocation.MatchesAnonymisedState(
            2,
            pseudonym,
            Now));
        Assert.Equal(
            InventoryDomainErrors.AllocationAlreadyAnonymised,
            allocation.Release(Guid.NewGuid(), 2, Now).Error);
        Assert.Equal(
            InventoryDomainErrors.AllocationAlreadyAnonymised,
            allocation.Amend(
                Guid.NewGuid(),
                2,
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 8, 4),
                [Guid.NewGuid()]).Error);
    }

    [Fact]
    public void Restore_replays_the_resulting_state_only_from_pre_state()
    {
        InventoryAllocation allocation = CreateRejected();
        Guid pseudonym = Guid.NewGuid();

        Result<InventoryAllocationAnonymisationOutcome> restored =
            allocation.RestoreAnonymisation(
                expectedResultingVersion: 2,
                pseudonym,
                Now);

        Assert.True(restored.IsSuccess);
        Assert.True(allocation.MatchesAnonymisedState(
            2,
            pseudonym,
            Now));
        Assert.True(allocation.RestoreAnonymisation(
            2,
            pseudonym,
            Now).IsFailure);
    }

    [Fact]
    public void Owner_and_restore_proofs_are_canonical_and_linked()
    {
        InventoryAllocation allocation = CreateRejected();
        Guid receiptId = Guid.NewGuid();
        Guid pseudonym = Guid.NewGuid();
        InventoryAllocationAnonymisationOutcome outcome =
            allocation.Anonymise(
                allocation.Version,
                pseudonym,
                Now).Value;
        InventoryAllocationAnonymisationReceipt receipt =
            InventoryAllocationAnonymisationReceipt.Create(
                receiptId,
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                allocation.PropertyId,
                Guid.NewGuid(),
                approvalRevision: 4,
                operationRevision: 5,
                allocation.Id,
                outcome,
                removedAmendmentDecisionCount: 2,
                new string('a', 64),
                "user:privacy-executor").Value;
        InventoryAllocationAnonymisationTombstone tombstone =
            InventoryAllocationAnonymisationTombstone.Create(
                receipt).Value;
        Guid ledgerEntryId = Guid.NewGuid();
        DateTimeOffset replayedAt = Now.AddMinutes(1);

        Assert.True(tombstone.Matches(receipt));
        Assert.Equal(64, receipt.CanonicalSha256.Length);
        Assert.Equal(
            receipt.CanonicalSha256,
            receipt.CanonicalSha256.ToLowerInvariant());
        Assert.True(tombstone.AttachRestoreProof(
            allocation.PropertyId,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingAllocationVersion,
            receipt.ResultingReservationPseudonym,
            allocationPresent: true,
            receipt.CompletedAtUtc,
            ledgerEntryId,
            replayedAt).IsSuccess);
        InventoryAllocationAnonymisationRestoreReceipt restoreReceipt =
            InventoryAllocationAnonymisationRestoreReceipt.Create(
                "tenant-a",
                ledgerEntryId,
                tenantSequence: 12,
                new string('b', 64),
                allocation.PropertyId,
                allocation.Id,
                receipt.ContractVersion,
                receipt.Id,
                receipt.CanonicalSha256,
                receipt.ResultingAllocationVersion,
                receipt.ResultingReservationPseudonym,
                allocationPresent: true,
                receipt.CompletedAtUtc,
                tombstone.Revision,
                replayedAt).Value;

        Assert.True(restoreReceipt.Matches(
            "tenant-a",
            ledgerEntryId,
            12,
            new string('b', 64),
            allocation.PropertyId,
            allocation.Id,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingAllocationVersion,
            receipt.CompletedAtUtc));
    }

    private static InventoryAllocation CreateActive() =>
        InventoryAllocation.CreateAccepted(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            Now.AddDays(-1)).Value;

    private static InventoryAllocation CreateRejected() =>
        InventoryAllocation.CreateRejected(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            InventoryAllocationRejection.UnitNotSellable,
            Now.AddDays(-1)).Value;
}
