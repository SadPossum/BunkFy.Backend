namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationProcessTests
{
    private const string TenantId = "tenant-a";
    private const string Approver = "owner:approver";
    private const string Executor = "owner:executor";
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Exporting_process_advances_monotonically_and_requires_distinct_destructive_executor()
    {
        TenantTerminationProcess process = Prepare(exportRequested: true);

        Assert.Equal(TenantTerminationProcessPhase.Freeze, process.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Pending, process.Status);

        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Approver,
            Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(1, process.OperationRevision);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            process.OperationRevision,
            process.Version,
            Approver,
            Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(TenantTerminationProcessPhase.Export, process.Phase);

        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Export,
            process.Version,
            Approver,
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Export,
            process.OperationRevision,
            process.Version,
            Approver,
            Now.AddMinutes(4)).IsSuccess);
        Assert.Equal(TenantTerminationProcessPhase.Destroy, process.Phase);

        Result denied = process.BeginPhase(
            TenantTerminationProcessPhase.Destroy,
            process.Version,
            Approver,
            Now.AddMinutes(5));
        Assert.True(denied.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationExecutorInvalid,
            denied.Error);

        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Destroy,
            process.Version,
            Executor,
            Now.AddMinutes(5)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Destroy,
            process.OperationRevision,
            process.Version,
            Executor,
            Now.AddMinutes(6)).IsSuccess);
        Assert.Equal(TenantTerminationProcessPhase.Verify, process.Phase);

        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Verify,
            process.Version,
            Executor,
            Now.AddMinutes(7)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Verify,
            process.OperationRevision,
            process.Version,
            Executor,
            Now.AddMinutes(8)).IsSuccess);
        Assert.Equal(TenantTerminationProcessPhase.Completed, process.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Completed, process.Status);
    }

    [Fact]
    public void Blocked_transition_preserves_version_conflict()
    {
        TenantTerminationProcess process = Prepare(exportRequested: false);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Approver,
            Now.AddMinutes(1)).IsSuccess);

        Result conflict = process.RecordBlocked(
            TenantTerminationProcessPhase.Freeze,
            process.OperationRevision,
            "workspace.legal-hold",
            Now.AddDays(1),
            expectedVersion: process.Version - 1,
            Approver,
            Now.AddMinutes(2));

        Assert.True(conflict.IsFailure);
        Assert.Equal(DataRightsDomainErrors.VersionConflict, conflict.Error);
        Assert.Equal(TenantTerminationProcessStatus.Running, process.Status);
    }

    [Fact]
    public void Non_exporting_process_moves_from_freeze_to_destroy()
    {
        TenantTerminationProcess process = Prepare(exportRequested: false);

        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Approver,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            process.OperationRevision,
            process.Version,
            Approver,
            Now.AddMinutes(2)).IsSuccess);

        Assert.Equal(TenantTerminationProcessPhase.Destroy, process.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Pending, process.Status);
    }

    [Fact]
    public void Blocked_phase_retains_only_bounded_review_evidence_and_requeues_in_place()
    {
        TenantTerminationProcess process = Prepare(exportRequested: false);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Approver,
            Now.AddMinutes(1)).IsSuccess);

        DateTimeOffset reviewAt = Now.AddDays(1);
        Assert.True(process.RecordBlocked(
            TenantTerminationProcessPhase.Freeze,
            process.OperationRevision,
            "workspace.legal-hold",
            reviewAt,
            process.Version,
            Approver,
            Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(TenantTerminationProcessStatus.Blocked, process.Status);
        Assert.Equal("workspace.legal-hold", process.OutcomeCode);
        Assert.Equal(reviewAt, process.HoldReviewAtUtc);

        Assert.True(process.Requeue(
            process.Version,
            Approver,
            Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(TenantTerminationProcessStatus.Pending, process.Status);
        Assert.Null(process.OutcomeCode);
        Assert.Null(process.HoldReviewAtUtc);
        Assert.Equal(TenantTerminationProcessPhase.Freeze, process.Phase);
    }

    [Fact]
    public void Exact_phase_delivery_replays_without_advancing_version()
    {
        TenantTerminationProcess process = Prepare(exportRequested: false);
        long prepareVersion = process.Version;
        DateTimeOffset startedAt = Now.AddMinutes(1);

        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            prepareVersion,
            Approver,
            startedAt).IsSuccess);
        long runningVersion = process.Version;
        long operationRevision = process.OperationRevision;
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            prepareVersion,
            Approver,
            startedAt).IsSuccess);
        Assert.Equal(runningVersion, process.Version);
        Assert.Equal(operationRevision, process.OperationRevision);

        DateTimeOffset completedAt = Now.AddMinutes(2);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            operationRevision,
            runningVersion,
            Approver,
            completedAt).IsSuccess);
        long completedVersion = process.Version;
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            operationRevision,
            runningVersion,
            Approver,
            completedAt).IsSuccess);
        Assert.Equal(completedVersion, process.Version);
    }

    [Fact]
    public void Stale_operation_result_cannot_complete_a_retried_phase()
    {
        TenantTerminationProcess process = Prepare(exportRequested: false);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Approver,
            Now.AddMinutes(1)).IsSuccess);
        long staleOperationRevision = process.OperationRevision;
        Assert.True(process.RecordFailed(
            TenantTerminationProcessPhase.Freeze,
            staleOperationRevision,
            "workspaces.retry-exhausted",
            process.Version,
            Approver,
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(process.Requeue(
            process.Version,
            Approver,
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Approver,
            Now.AddMinutes(4)).IsSuccess);

        Result stale = process.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            staleOperationRevision,
            process.Version,
            Approver,
            Now.AddMinutes(5));

        Assert.True(stale.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationTransitionInvalid,
            stale.Error);
        Assert.Equal(TenantTerminationProcessStatus.Running, process.Status);
    }

    [Fact]
    public void Cancellation_requires_restore_completion_after_freeze()
    {
        TenantTerminationProcess process = Prepare(exportRequested: false);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Approver,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            process.OperationRevision,
            process.Version,
            Approver,
            Now.AddMinutes(2)).IsSuccess);

        Assert.True(process.RequestCancellation(
            process.Version,
            Approver,
            Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(TenantTerminationProcessPhase.Restore, process.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Pending, process.Status);

        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Restore,
            process.Version,
            Approver,
            Now.AddMinutes(4)).IsSuccess);
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Restore,
            process.OperationRevision,
            process.Version,
            Approver,
            Now.AddMinutes(5)).IsFailure);
        Assert.Equal(TenantTerminationProcessStatus.Running, process.Status);

        Assert.True(process.CompleteCancellation(
            process.OperationRevision,
            process.Version,
            Approver,
            Now.AddMinutes(5)).IsSuccess);
        Assert.Equal(TenantTerminationProcessPhase.Restore, process.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Cancelled, process.Status);
    }

    [Fact]
    public void Cancellation_is_denied_before_freeze_or_after_destruction_starts()
    {
        TenantTerminationProcess beforeFreeze = Prepare(exportRequested: false);
        Assert.True(beforeFreeze.RequestCancellation(
            beforeFreeze.Version,
            Approver,
            Now.AddMinutes(1)).IsFailure);

        TenantTerminationProcess destroying = Prepare(exportRequested: false);
        Assert.True(destroying.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            destroying.Version,
            Approver,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(destroying.CompletePhase(
            TenantTerminationProcessPhase.Freeze,
            destroying.OperationRevision,
            destroying.Version,
            Approver,
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(destroying.BeginPhase(
            TenantTerminationProcessPhase.Destroy,
            destroying.Version,
            Executor,
            Now.AddMinutes(3)).IsSuccess);

        Result denied = destroying.RequestCancellation(
            destroying.Version,
            Approver,
            Now.AddMinutes(4));

        Assert.True(denied.IsFailure);
        Assert.Equal(
            DataRightsDomainErrors.TenantTerminationTransitionInvalid,
            denied.Error);
        Assert.Equal(TenantTerminationProcessStatus.Running, destroying.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ABC")]
    [InlineData("contains space")]
    public void Invalid_policy_or_actor_coordinates_fail_closed(
        string invalidDigestOrActor)
    {
        Result<TenantTerminationProcess> result =
            TenantTerminationProcess.Prepare(
                Guid.NewGuid(),
                TenantId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                approvalRevision: 4,
                Guid.NewGuid(),
                exportRequested: false,
                invalidDigestOrActor,
                Approver,
                Now,
                invalidDigestOrActor,
                Now);

        Assert.True(result.IsFailure);
    }

    private static TenantTerminationProcess Prepare(bool exportRequested) =>
        TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 4,
            Guid.NewGuid(),
            exportRequested,
            Digest,
            Approver,
            Now,
            "system:tenant-termination",
            Now).Value;
}
