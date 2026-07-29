namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
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
        DataRightsExecutionScope executionScope =
            DataRightsExecutionScope.ForProperty(propertyId);

        DataRightsExecutionBatch batch = DataRightsExecutionBatch.Prepare(
            id,
            " tenant-a ",
            idempotencyKey,
            caseId,
            executionScope,
            approvalRevision: 7,
            executionRevision: 8,
            selectedSubjectCount: 3,
            " user:executor ",
            Now).Value;

        Assert.Equal("tenant-a", batch.ScopeId);
        Assert.Equal(3, batch.SelectedSubjectCount);
        Assert.Equal("user:executor", batch.CreatedBy);
        Assert.True(batch.Matches(
            idempotencyKey,
            caseId,
            executionScope,
            8));
        Assert.False(batch.Matches(
            Guid.NewGuid(),
            caseId,
            executionScope,
            8));
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
                DataRightsExecutionScope.ForProperty(Guid.NewGuid()),
                approvalRevision: 7,
                executionRevision: 8,
                selectedSubjectCount,
                "user:executor",
                Now).Error.Code);
    }

    [Fact]
    public void Preparation_supports_staff_tenant_scope()
    {
        DataRightsExecutionBatch batch = DataRightsExecutionBatch.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            DataRightsExecutionScope.Staff,
            approvalRevision: 7,
            executionRevision: 8,
            selectedSubjectCount: 1,
            "user:executor",
            Now).Value;

        Assert.Equal(
            DataRightsCaseKind.StaffRights,
            batch.CaseKind);
        Assert.Equal(
            DataRightsCaseScopeKind.Tenant,
            batch.ScopeKind);
        Assert.Null(batch.PropertyId);
        Assert.True(batch.Matches(
            batch.IdempotencyKey,
            batch.CaseId,
            DataRightsExecutionScope.Staff,
            batch.ExecutionRevision));
    }
}
