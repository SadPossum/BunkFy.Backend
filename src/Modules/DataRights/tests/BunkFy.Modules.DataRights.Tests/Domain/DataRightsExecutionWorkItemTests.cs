namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsExecutionWorkItemTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Preparation_freezes_subject_policy_and_execution_coordinates()
    {
        Guid propertyId = Guid.NewGuid();
        Guid idempotencyKey = Guid.NewGuid();
        DataRightsSubjectCoordinate subject = DataRightsSubjectCoordinate.Create(
            " Guests ",
            " Guest-Profile ",
            Guid.NewGuid(),
            4,
            "user:selector",
            Now).Value;
        DataRightsApprovalPolicyEvidence policy = CreatePolicy(propertyId);

        DataRightsExecutionWorkItem workItem = DataRightsExecutionWorkItem.Prepare(
            Guid.NewGuid(),
            " tenant-a ",
            idempotencyKey,
            Guid.NewGuid(),
            propertyId,
            approvalRevision: 6,
            executionRevision: 7,
            DataRightsCaseOperation.Anonymisation,
            subject,
            policy,
            " user:executor ",
            Now.AddMinutes(1)).Value;

        Assert.Equal("tenant-a", workItem.ScopeId);
        Assert.Equal(DataRightsExecutionWorkItemState.Prepared, workItem.State);
        Assert.Equal(0, workItem.AttemptCount);
        Assert.Equal("guests", workItem.OwnerKey);
        Assert.Equal("guest-profile", workItem.RecordType);
        Assert.Equal(subject.RecordId, workItem.RecordId);
        Assert.Equal(4, workItem.SelectedRecordVersion);
        Assert.Equal("approved-policy", workItem.PolicyId);
        Assert.Equal(3, workItem.PolicyVersion);
        Assert.Equal("guest-retention", workItem.RetentionPolicyId);
        Assert.Equal(2, workItem.RetentionPolicyVersion);
        Assert.Equal(new string('a', 64), workItem.PolicyContentSha256);
        Assert.Equal("user:executor", workItem.CreatedBy);
        Assert.True(workItem.HasIdempotencyKey(idempotencyKey));
        Assert.False(workItem.HasIdempotencyKey(Guid.NewGuid()));
    }

    [Fact]
    public void Preparation_rejects_non_anonymisation_and_mismatched_policy_scope()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsSubjectCoordinate subject = DataRightsSubjectCoordinate.Create(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            1,
            "user:selector",
            Now).Value;

        Assert.Equal(
            "DataRights.ExecutionCoordinateInvalid",
            DataRightsExecutionWorkItem.Prepare(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                propertyId,
                6,
                7,
                DataRightsCaseOperation.Correction,
                subject,
                CreatePolicy(propertyId),
                "user:executor",
                Now).Error.Code);
        Assert.Equal(
            "DataRights.ExecutionCoordinateInvalid",
            DataRightsExecutionWorkItem.Prepare(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                propertyId,
                6,
                7,
                DataRightsCaseOperation.Anonymisation,
                subject,
                CreatePolicy(Guid.NewGuid()),
                "user:executor",
                Now).Error.Code);
    }

    private static DataRightsApprovalPolicyEvidence CreatePolicy(Guid propertyId) =>
        DataRightsApprovalPolicyEvidence.Create(
            propertyId,
            9,
            "GB",
            "approved-policy",
            3,
            "guest-retention",
            2,
            new string('a', 64),
            "data-rights-anonymisation",
            "erasure",
            "authorized-workspace-operator",
            Now).Value;
}
