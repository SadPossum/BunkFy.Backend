namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffRetentionCorrelationReceiptTests
{
    private static readonly Guid ReceiptId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ExecutionId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid StaffMemberId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private const string TenantId =
        "40000000-0000-0000-0000-000000000001";
    private static readonly DateTimeOffset CompletedAt =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.FromHours(3));

    [Fact]
    public void Receipt_has_deterministic_proof_and_opaque_subject()
    {
        WorkspaceStaffRetentionCorrelationReceipt receipt =
            Create().Value;
        WorkspaceStaffRetentionCorrelationReceipt replay =
            Create().Value;

        Assert.True(receipt.HasValidCanonicalProof());
        Assert.True(receipt.Matches(
            $" {TenantId} ",
            StaffMemberId,
            selectedStaffVersion: 7));
        Assert.Equal(replay.CanonicalSha256, receipt.CanonicalSha256);
        Assert.Equal(
            WorkspaceStaffRetentionCorrelationReceipt.Sha256Length,
            receipt.CanonicalSha256.Length);
        Assert.Equal(TimeSpan.Zero, receipt.CompletedAtUtc.Offset);
        Assert.Equal(
            $"retained:{ReceiptId:N}",
            receipt.CreateSubjectPseudonym());
        Assert.DoesNotContain(
            StaffMemberId.ToString("N"),
            receipt.CreateSubjectPseudonym(),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Receipt_rejects_invalid_coordinates_and_counts()
    {
        AssertFailure(Create(receiptId: Guid.Empty));
        AssertFailure(Create(executionId: Guid.Empty));
        AssertFailure(Create(staffMemberId: Guid.Empty));
        AssertFailure(Create(selectedStaffVersion: 0));
        AssertFailure(Create(onboardingRecordsScrubbed: -1));
        AssertFailure(
            WorkspaceStaffRetentionCorrelationReceipt.Create(
                ReceiptId,
                TenantId,
                ExecutionId,
                StaffMemberId,
                selectedStaffVersion: 7,
                onboardingRecordsScrubbed: 2,
                accessProcessRecordsScrubbed: 3,
                accessPlanRecordsScrubbed: 4,
                completedAtUtc: default));
        AssertFailure(Create(tenantId: "tenant with spaces"));
    }

    [Fact]
    public void Canonical_proof_detects_persisted_tampering()
    {
        WorkspaceStaffRetentionCorrelationReceipt receipt =
            Create().Value;
        typeof(WorkspaceStaffRetentionCorrelationReceipt)
            .GetProperty(
                nameof(
                    WorkspaceStaffRetentionCorrelationReceipt
                        .CanonicalSha256))!
            .SetValue(
                receipt,
                new string(
                    'a',
                    WorkspaceStaffRetentionCorrelationReceipt
                        .Sha256Length));

        Assert.False(receipt.HasValidCanonicalProof());
        Assert.False(receipt.Matches(
            TenantId,
            StaffMemberId,
            selectedStaffVersion: 7));
    }

    private static Result<WorkspaceStaffRetentionCorrelationReceipt>
        Create(
            Guid? receiptId = null,
            string tenantId = TenantId,
            Guid? executionId = null,
            Guid? staffMemberId = null,
            long selectedStaffVersion = 7,
            int onboardingRecordsScrubbed = 2,
            DateTimeOffset? completedAtUtc = null) =>
        WorkspaceStaffRetentionCorrelationReceipt.Create(
            receiptId ?? ReceiptId,
            tenantId,
            executionId ?? ExecutionId,
            staffMemberId ?? StaffMemberId,
            selectedStaffVersion,
            onboardingRecordsScrubbed,
            accessProcessRecordsScrubbed: 3,
            accessPlanRecordsScrubbed: 4,
            completedAtUtc ?? CompletedAt);

    private static void AssertFailure(
        Result<WorkspaceStaffRetentionCorrelationReceipt> result)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(
            WorkspaceStaffRetentionErrors.ReceiptInvalid,
            result.Error);
    }
}
