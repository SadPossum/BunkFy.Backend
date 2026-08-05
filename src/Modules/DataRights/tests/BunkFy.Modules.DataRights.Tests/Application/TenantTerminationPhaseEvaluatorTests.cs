namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationPhaseEvaluatorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 15, 0, 0, TimeSpan.Zero);
    private static readonly string Digest = new('a', 64);

    [Fact]
    public void Prepared_and_retry_work_return_only_dependency_ready_dispatches()
    {
        Fixture fixture = CreateFixture(
            [Stub("workspaces"), Stub("reservations", ["workspaces"])]);

        TenantTerminationPhaseEvaluation initial = fixture.Evaluator
            .Evaluate(fixture.Process, fixture.WorkItems).Value;
        Complete(fixture.WorkItems.Single(item =>
            item.OwnerKey == "workspaces"), sequence: 1);
        TenantTerminationPhaseEvaluation dependent = fixture.Evaluator
            .Evaluate(fixture.Process, fixture.WorkItems).Value;

        Assert.Equal(
            TenantTerminationPhaseDisposition.Running,
            initial.Disposition);
        Assert.Equal(
            "workspaces",
            Assert.Single(initial.ReadyDispatches).OwnerKey);
        Assert.Equal(
            "reservations",
            Assert.Single(dependent.ReadyDispatches).OwnerKey);
    }

    [Fact]
    public void All_completed_work_completes_the_phase()
    {
        Fixture fixture = CreateFixture(
            [Stub("workspaces"), Stub("reservations")]);
        Complete(fixture.WorkItems[0], sequence: 1);
        Complete(fixture.WorkItems[1], sequence: 2);

        TenantTerminationPhaseEvaluation evaluation = fixture.Evaluator
            .Evaluate(fixture.Process, fixture.WorkItems).Value;

        Assert.Equal(
            TenantTerminationPhaseDisposition.Completed,
            evaluation.Disposition);
        Assert.Empty(evaluation.ReadyDispatches);
    }

    [Fact]
    public void Blocked_work_waits_for_processing_work_then_fails_stopped()
    {
        Fixture fixture = CreateFixture(
            [Stub("inventory"), Stub("reservations")]);
        Block(
            fixture.WorkItems.Single(item => item.OwnerKey == "inventory"),
            sequence: 1,
            reviewAtUtc: Now.AddDays(2));
        Begin(
            fixture.WorkItems.Single(item => item.OwnerKey == "reservations"),
            sequence: 2);

        TenantTerminationPhaseEvaluation inFlight = fixture.Evaluator
            .Evaluate(fixture.Process, fixture.WorkItems).Value;
        Complete(
            fixture.WorkItems.Single(item => item.OwnerKey == "reservations"),
            sequence: 2,
            taskAlreadyStarted: true);
        TenantTerminationPhaseEvaluation settled = fixture.Evaluator
            .Evaluate(fixture.Process, fixture.WorkItems).Value;

        Assert.Equal(
            TenantTerminationPhaseDisposition.Running,
            inFlight.Disposition);
        Assert.Empty(inFlight.ReadyDispatches);
        Assert.Equal(
            TenantTerminationPhaseDisposition.Blocked,
            settled.Disposition);
        Assert.Equal(
            TenantTerminationPhaseEvaluator.BlockedOutcomeCode,
            settled.OutcomeCode);
        Assert.Equal(Now.AddDays(2), settled.HoldReviewAtUtc);
    }

    [Fact]
    public void Failed_work_takes_precedence_after_in_flight_work_settles()
    {
        Fixture fixture = CreateFixture(
            [Stub("inventory"), Stub("reservations")]);
        Block(
            fixture.WorkItems.Single(item => item.OwnerKey == "inventory"),
            sequence: 1,
            reviewAtUtc: null);
        Fail(
            fixture.WorkItems.Single(item => item.OwnerKey == "reservations"),
            sequence: 2);

        TenantTerminationPhaseEvaluation evaluation = fixture.Evaluator
            .Evaluate(fixture.Process, fixture.WorkItems).Value;

        Assert.Equal(
            TenantTerminationPhaseDisposition.Failed,
            evaluation.Disposition);
        Assert.Equal(
            TenantTerminationPhaseEvaluator.FailedOutcomeCode,
            evaluation.OutcomeCode);
        Assert.Null(evaluation.HoldReviewAtUtc);
        Assert.Empty(evaluation.ReadyDispatches);
    }

    [Fact]
    public void Retry_required_work_receives_a_new_deterministic_dispatch()
    {
        Fixture fixture = CreateFixture([Stub("reservations")]);
        Retry(Assert.Single(fixture.WorkItems), sequence: 1);

        TenantTerminationPhaseEvaluation evaluation = fixture.Evaluator
            .Evaluate(fixture.Process, fixture.WorkItems).Value;
        TenantTerminationPlannedDispatch continuation = Assert.Single(
            evaluation.ReadyDispatches);

        Assert.Equal(
            TenantTerminationPhaseDisposition.Running,
            evaluation.Disposition);
        Assert.Equal(2, continuation.DispatchSequence);
    }

    private static Fixture CreateFixture(
        IReadOnlyCollection<ITenantTerminationContributor> contributors)
    {
        TenantTerminationProcess process = CreateRunningDestroyProcess();
        TenantTerminationPhasePlanner planner = new(contributors);
        List<TenantTerminationOwnerWorkItem> workItems =
            [.. planner.PrepareWorkItems(process, Now.AddMinutes(4)).Value];
        return new(
            process,
            workItems,
            new TenantTerminationPhaseEvaluator(planner));
    }

    private static TenantTerminationProcess CreateRunningDestroyProcess()
    {
        TenantTerminationProcess process = TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 4,
            Guid.NewGuid(),
            exportRequested: false,
            Digest,
            "approver",
            Now,
            "creator",
            Now).Value;
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            "executor",
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(TenantTerminationTestFixture.CompleteFreeze(
            process,
            process.OperationRevision,
            process.Version,
            "executor",
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Destroy,
            process.Version,
            "executor",
            Now.AddMinutes(3)).IsSuccess);
        return process;
    }

    private static void Begin(
        TenantTerminationOwnerWorkItem item,
        int sequence)
    {
        Guid runId = RunId(item, sequence);
        Assert.True(item.BeginProcessing(
            runId,
            taskAttempt: 1,
            item.Version,
            Now.AddMinutes(5).AddSeconds(sequence)).IsSuccess);
    }

    private static void Complete(
        TenantTerminationOwnerWorkItem item,
        int sequence,
        bool taskAlreadyStarted = false)
    {
        if (!taskAlreadyStarted)
        {
            Begin(item, sequence);
        }

        Assert.True(item.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            $"{item.OwnerKey}.completed",
            affectedCount: 1,
            retainedMinimumCount: 0,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision: 0,
            resultingProofRevision: 1,
            item.CatalogVersion,
            item.CatalogSha256,
            item.TaskRunId!.Value,
            item.LastTaskAttempt,
            item.Version,
            item.LastChangedAtUtc.AddSeconds(1)).IsSuccess);
    }

    private static void Block(
        TenantTerminationOwnerWorkItem item,
        int sequence,
        DateTimeOffset? reviewAtUtc)
    {
        Begin(item, sequence);
        Assert.True(item.RecordResult(
            TenantTerminationOwnerWorkState.Blocked,
            $"{item.OwnerKey}.blocked",
            affectedCount: 0,
            retainedMinimumCount: 0,
            remainingActiveCount: 1,
            reviewAtUtc,
            selectedProofRevision: null,
            resultingProofRevision: null,
            item.CatalogVersion,
            item.CatalogSha256,
            item.TaskRunId!.Value,
            item.LastTaskAttempt,
            item.Version,
            item.LastChangedAtUtc.AddSeconds(1)).IsSuccess);
    }

    private static void Fail(
        TenantTerminationOwnerWorkItem item,
        int sequence)
    {
        Begin(item, sequence);
        Assert.True(item.RecordResult(
            TenantTerminationOwnerWorkState.Failed,
            $"{item.OwnerKey}.failed",
            affectedCount: 0,
            retainedMinimumCount: 0,
            remainingActiveCount: 1,
            holdReviewAtUtc: null,
            selectedProofRevision: null,
            resultingProofRevision: null,
            item.CatalogVersion,
            item.CatalogSha256,
            item.TaskRunId!.Value,
            item.LastTaskAttempt,
            item.Version,
            item.LastChangedAtUtc.AddSeconds(1)).IsSuccess);
    }

    private static void Retry(
        TenantTerminationOwnerWorkItem item,
        int sequence)
    {
        Begin(item, sequence);
        Assert.True(item.RecordResult(
            TenantTerminationOwnerWorkState.RetryRequired,
            $"{item.OwnerKey}.more-work",
            affectedCount: 500,
            retainedMinimumCount: 0,
            remainingActiveCount: 1,
            holdReviewAtUtc: null,
            selectedProofRevision: null,
            resultingProofRevision: null,
            item.CatalogVersion,
            item.CatalogSha256,
            item.TaskRunId!.Value,
            item.LastTaskAttempt,
            item.Version,
            item.LastChangedAtUtc.AddSeconds(1)).IsSuccess);
    }

    private static Guid RunId(
        TenantTerminationOwnerWorkItem item,
        int sequence) =>
        BunkFy.Modules.DataRights.Application
            .TenantTerminationExecutionIdentity.CreateTaskRunId(
                item.Id,
                sequence);

    private static StubContributor Stub(
        string ownerKey,
        IReadOnlyCollection<string>? dependencies = null) =>
        new(new(
            ownerKey,
            TenantTerminationContract.CurrentVersion,
            [
                new(
                    TenantTerminationContributionPhase.Destroy,
                    dependencies ?? [])
            ],
            MandatoryForProduction: true,
            CatalogVersion: 2,
            CatalogSha256: Digest));

    private sealed class StubContributor(
        TenantTerminationContributorDescriptor descriptor)
        : ITenantTerminationContributor
    {
        public TenantTerminationContributorDescriptor Descriptor { get; } =
            descriptor;

        public Task<TenantTerminationContributionResult> ExecuteAsync(
            TenantTerminationContributionRequest request,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed record Fixture(
        TenantTerminationProcess Process,
        List<TenantTerminationOwnerWorkItem> WorkItems,
        TenantTerminationPhaseEvaluator Evaluator);
}
