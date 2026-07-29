namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Domain.DataRights;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffAnonymisationProofTests
{
    private static readonly DateTimeOffset CompletedAtUtc =
        new(2026, 7, 30, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Receipt_and_tombstone_bind_exact_owner_coordinates()
    {
        Guid staffMemberId = Guid.NewGuid();
        var created = StaffAnonymisationReceipt.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 7,
            operationRevision: 8,
            staffMemberId,
            selectedStaffVersion: 4,
            resultingStaffVersion: 5,
            selectedOperationLockRevision: 3,
            resultingOperationLockRevision: 4,
            new string('a', 64),
            new string('b', 64),
            Guid.NewGuid(),
            "user:privacy",
            CompletedAtUtc);

        Assert.True(created.IsSuccess, created.Error.Code);
        StaffAnonymisationReceipt receipt = created.Value;
        Assert.Equal(64, receipt.CanonicalSha256.Length);
        Assert.True(receipt.Matches(
            receipt.IdempotencyKey,
            receipt.CaseId,
            receipt.ApprovalRevision,
            receipt.OperationRevision,
            receipt.StaffMemberId,
            receipt.SelectedStaffVersion,
            receipt.ApprovalEvidenceSha256,
            receipt.ActorId));

        var tombstone = StaffAnonymisationTombstone.Create(
            receipt.ScopeId,
            staffMemberId,
            receipt.CompletedAtUtc,
            receipt.CanonicalSha256);

        Assert.True(tombstone.IsSuccess, tombstone.Error.Code);
        Assert.True(tombstone.Value.Matches(receipt));
    }

    [Fact]
    public void Receipt_rejects_non_monotonic_versions()
    {
        var created = StaffAnonymisationReceipt.Create(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 7,
            operationRevision: 8,
            Guid.NewGuid(),
            selectedStaffVersion: 4,
            resultingStaffVersion: 6,
            selectedOperationLockRevision: 3,
            resultingOperationLockRevision: 4,
            new string('a', 64),
            new string('b', 64),
            Guid.NewGuid(),
            "user:privacy",
            CompletedAtUtc);

        Assert.Equal(
            "Staff.AnonymisationReceiptInvalid",
            created.Error.Code);
    }
}
