namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsExecutionBatchTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Preparation_freezes_idempotency_revisions_and_subject_count()
    {
        Guid id = Guid.NewGuid();
        Guid idempotencyKey = Guid.NewGuid();
        Guid caseId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();

        DataRightsExecutionBatch batch = DataRightsExecutionBatch.Prepare(
            id,
            " tenant-a ",
            idempotencyKey,
            caseId,
            propertyId,
            approvalRevision: 7,
            executionRevision: 8,
            selectedSubjectCount: 3,
            " user:executor ",
            Now).Value;

        Assert.Equal("tenant-a", batch.ScopeId);
        Assert.Equal(3, batch.SelectedSubjectCount);
        Assert.Equal("user:executor", batch.CreatedBy);
        Assert.True(batch.Matches(idempotencyKey, caseId, propertyId, 8));
        Assert.False(batch.Matches(Guid.NewGuid(), caseId, propertyId, 8));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Preparation_rejects_invalid_subject_count(int selectedSubjectCount)
    {
        Assert.Equal(
            "DataRights.ExecutionCoordinateInvalid",
            DataRightsExecutionBatch.Prepare(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                approvalRevision: 7,
                executionRevision: 8,
                selectedSubjectCount,
                "user:executor",
                Now).Error.Code);
    }
}
