namespace BunkFy.Modules.Workspaces.Tests.Domain;

using BunkFy.Modules.Workspaces.Domain.DataRights;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    WorkspaceStaffCorrelationAnonymisationProofTests
{
    private const string TenantId =
        "10000000-0000-0000-0000-000000000001";
    private static readonly DateTimeOffset CompletedAtUtc =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Receipt_and_tombstone_bind_versions_without_identity()
    {
        WorkspaceStaffCorrelationAnonymisationReceipt receipt =
            CreateReceipt();

        WorkspaceStaffCorrelationAnonymisationTombstone tombstone =
            WorkspaceStaffCorrelationAnonymisationTombstone
                .Create(receipt).Value;

        Assert.True(receipt.HasValidCanonicalProof());
        Assert.True(tombstone.Matches(receipt));
        Assert.Equal(
            $"anonymised:{receipt.Id:N}",
            receipt.CreateSubjectPseudonym());
        Assert.Equal(
            receipt.CreateSubjectPseudonym(),
            tombstone.CreateSubjectPseudonym());
        Assert.DoesNotContain(
            typeof(
                WorkspaceStaffCorrelationAnonymisationReceipt)
                .GetProperties(),
            property => property.Name.Contains(
                "Subject",
                StringComparison.Ordinal));
        Assert.DoesNotContain(
            typeof(
                WorkspaceStaffCorrelationAnonymisationTombstone)
                .GetProperties(),
            property => property.Name.Contains(
                "Subject",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Restore_proof_attachment_is_idempotent_and_exact()
    {
        WorkspaceStaffCorrelationAnonymisationReceipt receipt =
            CreateReceipt();
        WorkspaceStaffCorrelationAnonymisationTombstone tombstone =
            WorkspaceStaffCorrelationAnonymisationTombstone
                .Create(receipt).Value;
        Guid ledgerEntryId = Guid.NewGuid();
        DateTimeOffset replayedAtUtc =
            CompletedAtUtc.AddMinutes(10);

        Assert.True(tombstone.AttachRestoreProof(
            ledgerEntryId,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingAnchorVersion,
            receipt.CompletedAtUtc,
            replayedAtUtc).IsSuccess);
        Assert.True(tombstone.AttachRestoreProof(
            ledgerEntryId,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingAnchorVersion,
            receipt.CompletedAtUtc,
            replayedAtUtc.AddMinutes(1)).IsSuccess);
        Assert.True(tombstone.MatchesRestore(
            ledgerEntryId,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingAnchorVersion,
            receipt.CompletedAtUtc));
        Assert.True(tombstone.AttachRestoreProof(
            Guid.NewGuid(),
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingAnchorVersion,
            receipt.CompletedAtUtc,
            replayedAtUtc).IsFailure);
    }

    [Fact]
    public void Restore_receipt_has_canonical_owner_binding()
    {
        WorkspaceStaffCorrelationAnonymisationReceipt owner =
            CreateReceipt();
        Guid ledgerEntryId = Guid.NewGuid();

        WorkspaceStaffCorrelationAnonymisationRestoreReceipt
            restored =
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt
                .Create(
                    TenantId,
                    ledgerEntryId,
                    3,
                    new string('d', 64),
                    owner.AnchorProcessId,
                    owner.StaffMemberId,
                    owner.ContractVersion,
                    owner.Id,
                    owner.CanonicalSha256,
                    owner.ResultingAnchorVersion,
                    owner.ResultingStateSha256,
                    owner.OnboardingRecordsScrubbed,
                    owner.AccessProcessRecordsScrubbed,
                    owner.AccessPlanRecordsScrubbed,
                    owner.CompletedAtUtc,
                    tombstoneRevision: 2,
                    replayedAtUtc:
                        CompletedAtUtc.AddMinutes(10))
                .Value;

        Assert.True(restored.HasValidCanonicalProof());
        Assert.True(restored.Matches(
            TenantId,
            ledgerEntryId,
            3,
            new string('d', 64),
            owner.AnchorProcessId,
            owner.ContractVersion,
            owner.Id,
            owner.CanonicalSha256,
            owner.ResultingAnchorVersion,
            owner.CompletedAtUtc));
        Assert.Equal(64, restored.CanonicalSha256.Length);
    }

    private static
        WorkspaceStaffCorrelationAnonymisationReceipt
        CreateReceipt() =>
        WorkspaceStaffCorrelationAnonymisationReceipt.Create(
            Guid.Parse(
                "20000000-0000-0000-0000-000000000001"),
            TenantId,
            Guid.Parse(
                "30000000-0000-0000-0000-000000000001"),
            Guid.Parse(
                "40000000-0000-0000-0000-000000000001"),
            approvalRevision: 4,
            operationRevision: 5,
            Guid.Parse(
                "50000000-0000-0000-0000-000000000001"),
            Guid.Parse(
                "60000000-0000-0000-0000-000000000001"),
            selectedStaffVersion: 7,
            selectedAnchorVersion: 4,
            resultingAnchorVersion: 5,
            onboardingRecordsScrubbed: 1,
            accessProcessRecordsScrubbed: 2,
            accessPlanRecordsScrubbed: 1,
            new string('a', 64),
            new string('b', 64),
            new string('c', 64),
            "user:privacy-executor",
            CompletedAtUtc).Value;
}
