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
            Guid.NewGuid(),
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
        Assert.Equal(
            DataRightsExecutionWorkItem.CurrentOwnerContractVersion,
            workItem.OwnerContractVersion);
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
    public void Processing_attempts_are_bound_to_one_task_and_retry_monotonically()
    {
        DataRightsExecutionWorkItem workItem = CreateWorkItem();
        Guid taskRunId = Guid.NewGuid();

        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(DataRightsExecutionWorkItemState.Processing, workItem.State);
        Assert.Equal(1, workItem.AttemptCount);
        Assert.Equal(1, workItem.LastTaskAttempt);
        Assert.Equal(taskRunId, workItem.TaskRunId);
        long firstVersion = workItem.Version;

        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(firstVersion, workItem.Version);
        Assert.Equal(1, workItem.AttemptCount);

        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 2,
            Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(2, workItem.AttemptCount);
        Assert.Equal(2, workItem.LastTaskAttempt);
        Assert.Equal(firstVersion + 1, workItem.Version);

        Assert.Equal(
            "DataRights.ExecutionTaskConflict",
            workItem.BeginProcessing(
                Guid.NewGuid(),
                taskAttempt: 3,
                Now.AddMinutes(4)).Error.Code);
    }

    [Fact]
    public void Owner_proof_is_exactly_replayable_but_cannot_be_retargeted()
    {
        DataRightsExecutionWorkItem workItem = CreateWorkItem();
        Guid taskRunId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            Now.AddMinutes(2)).IsSuccess);

        Assert.True(workItem.RecordOwnerProof(
            workItem.Version,
            taskRunId,
            taskAttempt: 1,
            receiptContractVersion: 1,
            receiptId,
            resultingRecordVersion: 5,
            "guests.completed",
            "guests.profile-anonymised",
            new string('b', 64),
            Now.AddMinutes(3),
            Now.AddMinutes(4)).IsSuccess);
        Assert.Equal(
            DataRightsExecutionWorkItemState.OwnerProofRecorded,
            workItem.State);
        Assert.Equal(receiptId, workItem.OwnerReceiptId);
        Assert.Null(workItem.OutcomeCode);
        long proofVersion = workItem.Version;

        Assert.True(workItem.RecordOwnerProof(
            expectedVersion: 1,
            Guid.NewGuid(),
            taskAttempt: 99,
            receiptContractVersion: 1,
            receiptId,
            resultingRecordVersion: 5,
            "guests.completed",
            "guests.profile-anonymised",
            new string('b', 64),
            Now.AddMinutes(3),
            Now.AddMinutes(5)).IsSuccess);
        Assert.Equal(proofVersion, workItem.Version);

        Assert.Equal(
            "DataRights.ExecutionOwnerResultInvalid",
            workItem.RecordOwnerProof(
                proofVersion,
                taskRunId,
                taskAttempt: 1,
                receiptContractVersion: 1,
                Guid.NewGuid(),
                resultingRecordVersion: 5,
                "guests.completed",
                "guests.profile-anonymised",
                new string('b', 64),
                Now.AddMinutes(3),
                Now.AddMinutes(5)).Error.Code);

        Assert.True(workItem.CompleteAfterDurableLedger(
            proofVersion,
            Now.AddMinutes(5)).IsSuccess);
        Assert.Equal(DataRightsExecutionWorkItemState.Completed, workItem.State);
        long completedVersion = workItem.Version;
        Assert.True(workItem.CompleteAfterDurableLedger(
            expectedVersion: 1,
            Now.AddMinutes(6)).IsSuccess);
        Assert.Equal(completedVersion, workItem.Version);
    }

    [Fact]
    public void Stable_owner_blocker_is_terminal_and_keeps_no_owner_proof()
    {
        DataRightsExecutionWorkItem workItem = CreateWorkItem();
        Guid taskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            Now.AddMinutes(2)).IsSuccess);

        Assert.True(workItem.RecordBlocked(
            workItem.Version,
            taskRunId,
            taskAttempt: 1,
            "Guests.AnonymisationBlocked.ActiveDataHold",
            Now.AddMinutes(3)).IsSuccess);

        Assert.Equal(DataRightsExecutionWorkItemState.Blocked, workItem.State);
        Assert.True(workItem.IsOwnerDispatchTerminal);
        Assert.Equal(
            "Guests.AnonymisationBlocked.ActiveDataHold",
            workItem.OutcomeCode);
        Assert.Null(workItem.OwnerReceiptId);
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 2,
            Now.AddMinutes(4)).IsSuccess);
        Assert.Equal(1, workItem.AttemptCount);
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

    private static DataRightsExecutionWorkItem CreateWorkItem()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsSubjectCoordinate subject = DataRightsSubjectCoordinate.Create(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            4,
            "user:selector",
            Now).Value;
        return DataRightsExecutionWorkItem.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            propertyId,
            approvalRevision: 6,
            executionRevision: 7,
            DataRightsCaseOperation.Anonymisation,
            subject,
            CreatePolicy(propertyId),
            "user:executor",
            Now.AddMinutes(1)).Value;
    }
}
