namespace BunkFy.Modules.DataRights.Tests.Contracts;

using BunkFy.Modules.DataRights.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationApprovalEvidenceTests
{
    [Fact]
    public void Evidence_is_normalized_and_has_a_stable_canonical_digest()
    {
        Assert.True(TenantTerminationApprovalEvidenceContract.TryCreate(
            " change:42 ",
            new string('a', TenantTerminationContract.Sha256Length),
            " backup:42 ",
            " restore/42 ",
            " assurance_42 ",
            out TenantTerminationApprovalEvidence? evidence));

        Assert.NotNull(evidence);
        Assert.Equal("change:42", evidence.ApprovalReference);
        string first = evidence.ComputeSha256();
        string second = evidence.ComputeSha256();
        Assert.Equal(first, second);
        Assert.True(TenantTerminationApprovalEvidenceContract.IsSha256(first));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("ab")]
    [InlineData("secret?value")]
    public void Evidence_rejects_unbounded_or_secret_like_references(
        string reference)
    {
        Assert.False(TenantTerminationApprovalEvidenceContract.TryCreate(
            reference,
            new string('a', TenantTerminationContract.Sha256Length),
            "backup:42",
            "restore:42",
            "assurance:42",
            out _));
    }

    [Fact]
    public void Digest_changes_when_any_approved_coordinate_changes()
    {
        TenantTerminationApprovalEvidence first = Create(
            new string('a', TenantTerminationContract.Sha256Length));
        TenantTerminationApprovalEvidence second = Create(
            new string('b', TenantTerminationContract.Sha256Length));

        Assert.NotEqual(first.ComputeSha256(), second.ComputeSha256());
        Assert.False(TenantTerminationApprovalEvidenceContract
            .FixedTimeSha256Equals(
                first.ComputeSha256(),
                second.ComputeSha256()));
    }

    private static TenantTerminationApprovalEvidence Create(string catalog)
    {
        Assert.True(TenantTerminationApprovalEvidenceContract.TryCreate(
            "change:42",
            catalog,
            "backup:42",
            "restore:42",
            "assurance:42",
            out TenantTerminationApprovalEvidence? evidence));
        return evidence!;
    }
}
