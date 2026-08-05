namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationOwnerWorkItemTests
{
    private static readonly string Digest = new('b', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Completed_result_requires_zero_active_records_and_exact_owner_proof()
    {
        TenantTerminationOwnerWorkItem workItem = Prepare();
        Guid taskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(1)).IsSuccess);

        Result invalid = workItem.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            "guests.completed",
            affectedCount: 4,
            retainedMinimumCount: 1,
            remainingActiveCount: 1,
            holdReviewAtUtc: null,
            selectedProofRevision: 2,
            resultingProofRevision: 3,
            catalogVersion: 2,
            Digest,
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(2));
        Assert.True(invalid.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid,
            invalid.Error);

        DateTimeOffset completedAt = Now.AddMinutes(2);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            "guests.completed",
            affectedCount: 4,
            retainedMinimumCount: 1,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision: 2,
            resultingProofRevision: 3,
            catalogVersion: 2,
            Digest,
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            completedAt).IsSuccess);
        long completedVersion = workItem.Version;

        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            "guests.completed",
            affectedCount: 4,
            retainedMinimumCount: 1,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision: 2,
            resultingProofRevision: 3,
            catalogVersion: 2,
            Digest,
            taskRunId,
            taskAttempt: 1,
            expectedVersion: 1,
            completedAt).IsSuccess);
        Assert.Equal(completedVersion, workItem.Version);

        Result wrongTaskReplay = workItem.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            "guests.completed",
            affectedCount: 4,
            retainedMinimumCount: 1,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision: 2,
            resultingProofRevision: 3,
            catalogVersion: 2,
            Digest,
            Guid.NewGuid(),
            taskAttempt: 1,
            completedVersion,
            completedAt);
        Assert.True(wrongTaskReplay.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid,
            wrongTaskReplay.Error);
    }

    [Fact]
    public void Completed_result_accepts_an_exact_empty_scope_revision()
    {
        TenantTerminationOwnerWorkItem workItem = Prepare();
        Guid taskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(1)).IsSuccess);

        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            "organizations.empty-scope",
            affectedCount: 0,
            retainedMinimumCount: 0,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision: 0,
            resultingProofRevision: 0,
            catalogVersion: 2,
            Digest,
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(2)).IsSuccess);

        Assert.Equal(0, workItem.SelectedProofRevision);
        Assert.Equal(0, workItem.ResultingProofRevision);
    }

    [Fact]
    public void Blocked_result_can_be_requeued_without_changing_work_identity()
    {
        TenantTerminationOwnerWorkItem workItem = Prepare();
        Guid taskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.Blocked,
            "guests.legal-hold",
            affectedCount: 0,
            retainedMinimumCount: 0,
            remainingActiveCount: 1,
            holdReviewAtUtc: null,
            selectedProofRevision: null,
            resultingProofRevision: null,
            catalogVersion: 2,
            Digest,
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(2)).IsSuccess);

        Assert.True(workItem.Requeue(
            workItem.Version,
            Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(TenantTerminationOwnerWorkState.Prepared, workItem.State);
        Assert.Null(workItem.ResultCode);
        Assert.Null(workItem.TaskRunId);
        Assert.True(workItem.Matches(
            workItem.ProcessId,
            TenantTerminationOwnerPhase.Destroy,
            "guests",
            operationRevision: 2,
            workItem.IdempotencyKey));

        Assert.True(workItem.BeginProcessing(
            Guid.NewGuid(),
            taskAttempt: 2,
            workItem.Version,
            Now.AddMinutes(4)).IsSuccess);
        Assert.Equal(2, workItem.AttemptCount);
    }

    [Fact]
    public void Blocked_result_requires_a_remaining_active_record()
    {
        TenantTerminationOwnerWorkItem workItem = Prepare();
        Guid taskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(1)).IsSuccess);

        Result result = workItem.RecordResult(
            TenantTerminationOwnerWorkState.Blocked,
            "reservations.legal-hold",
            affectedCount: 0,
            retainedMinimumCount: 0,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision: null,
            resultingProofRevision: null,
            catalogVersion: 2,
            Digest,
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(2));

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid,
            result.Error);
    }

    [Fact]
    public void Catalog_or_attempt_mismatch_fails_closed()
    {
        TenantTerminationOwnerWorkItem workItem = Prepare();
        Guid taskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(1)).IsSuccess);

        Result result = workItem.RecordResult(
            TenantTerminationOwnerWorkState.RetryRequired,
            "guests.retry",
            affectedCount: 0,
            retainedMinimumCount: 0,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision: null,
            resultingProofRevision: null,
            catalogVersion: 3,
            new string('c', 64),
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(2));

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid,
            result.Error);
    }

    [Fact]
    public void Bounded_continuation_uses_a_new_task_run_starting_at_attempt_one()
    {
        TenantTerminationOwnerWorkItem workItem = Prepare();
        Guid firstTaskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            firstTaskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.RetryRequired,
            "guests.more-work",
            affectedCount: 500,
            retainedMinimumCount: 0,
            remainingActiveCount: 1,
            holdReviewAtUtc: null,
            selectedProofRevision: null,
            resultingProofRevision: null,
            catalogVersion: 2,
            Digest,
            firstTaskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(2)).IsSuccess);

        Guid continuationTaskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            continuationTaskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(2, workItem.AttemptCount);
        Assert.Equal(1, workItem.LastTaskAttempt);
        Assert.Equal(continuationTaskRunId, workItem.TaskRunId);

        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.RetryRequired,
            "guests.more-work",
            affectedCount: 500,
            retainedMinimumCount: 0,
            remainingActiveCount: 1,
            holdReviewAtUtc: null,
            selectedProofRevision: null,
            resultingProofRevision: null,
            catalogVersion: 2,
            Digest,
            continuationTaskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(4)).IsSuccess);
        Assert.True(workItem.BeginProcessing(
            continuationTaskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(5)).IsFailure);
        Assert.True(workItem.BeginProcessing(
            continuationTaskRunId,
            taskAttempt: 2,
            workItem.Version,
            Now.AddMinutes(5)).IsSuccess);
    }

    [Fact]
    public void Task_runtime_retry_advances_only_the_same_processing_run()
    {
        TenantTerminationOwnerWorkItem workItem = Prepare();
        Guid taskRunId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(1)).IsSuccess);
        long processingVersion = workItem.Version;

        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            expectedVersion: 1,
            Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(processingVersion, workItem.Version);
        Assert.True(workItem.BeginProcessing(
            Guid.NewGuid(),
            taskAttempt: 2,
            workItem.Version,
            Now.AddMinutes(2)).IsFailure);

        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 2,
            workItem.Version,
            Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(TenantTerminationOwnerWorkState.Processing, workItem.State);
        Assert.Equal(2, workItem.AttemptCount);
        Assert.Equal(2, workItem.LastTaskAttempt);
        Assert.Equal(taskRunId, workItem.TaskRunId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public void Undefined_owner_phase_fails_closed(int phase)
    {
        Result<TenantTerminationOwnerWorkItem> result =
            TenantTerminationOwnerWorkItem.Prepare(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                Guid.NewGuid(),
                approvalRevision: 7,
                operationRevision: 2,
                Guid.NewGuid(),
                Guid.NewGuid(),
                (TenantTerminationOwnerPhase)phase,
                "guests",
                ownerContractVersion: 1,
                catalogVersion: 2,
                Digest,
                new string('a', 64),
                Now);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationOwnerWorkInvalid,
            result.Error);
    }

    private static TenantTerminationOwnerWorkItem Prepare() =>
        TenantTerminationOwnerWorkItem.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 7,
            operationRevision: 2,
            Guid.NewGuid(),
            Guid.NewGuid(),
            TenantTerminationOwnerPhase.Destroy,
            "guests",
            ownerContractVersion: 1,
            catalogVersion: 2,
            Digest,
            new string('a', 64),
            Now).Value;
}
