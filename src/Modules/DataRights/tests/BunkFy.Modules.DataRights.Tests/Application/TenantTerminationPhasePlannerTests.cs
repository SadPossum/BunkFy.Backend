namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationPhasePlannerTests
{
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Execution_identities_are_stable_and_coordinate_separated()
    {
        Guid processId =
            Guid.Parse("11111111-2222-3333-4444-555555555555");

        Guid workItemId = TenantTerminationExecutionIdentity.CreateWorkItemId(
            processId,
            operationRevision: 5,
            TenantTerminationOwnerPhase.Destroy,
            "reservations");
        Guid repeated = TenantTerminationExecutionIdentity.CreateWorkItemId(
            processId,
            operationRevision: 5,
            TenantTerminationOwnerPhase.Destroy,
            "reservations");
        Guid idempotencyKey = TenantTerminationExecutionIdentity
            .CreateWorkItemIdempotencyKey(
                processId,
                operationRevision: 5,
                TenantTerminationOwnerPhase.Destroy,
                "reservations");
        Guid firstTask = TenantTerminationExecutionIdentity.CreateTaskRunId(
            workItemId,
            dispatchSequence: 1);
        Guid secondTask = TenantTerminationExecutionIdentity.CreateTaskRunId(
            workItemId,
            dispatchSequence: 2);

        Assert.Equal(workItemId, repeated);
        Assert.NotEqual(workItemId, idempotencyKey);
        Assert.NotEqual(firstTask, secondTask);
        Assert.NotEqual(
            workItemId,
            TenantTerminationExecutionIdentity.CreateWorkItemId(
                processId,
                operationRevision: 6,
                TenantTerminationOwnerPhase.Destroy,
                "reservations"));
        Assert.Equal(
            $"tenant-termination:{firstTask:N}",
            TenantTerminationExecutionIdentity
                .CreateTaskDeduplicationKey(firstTask));
    }

    [Fact]
    public void Planner_prepares_exact_owner_work_and_releases_only_ready_nodes()
    {
        TenantTerminationProcess process = CreateRunningDestroyProcess();
        TenantTerminationPhasePlanner planner = new(
        [
            Stub("root-b"),
            Stub("root-a"),
            Stub("product", ["root-a", "root-b"]),
            Stub(
                "task-runtime",
                ["product"],
                TenantTerminationExecutionBoundary.GlobalControlTask),
            Stub(
                "workspaces",
                ["task-runtime"],
                TenantTerminationExecutionBoundary.GlobalControlTask)
        ]);

        Result<IReadOnlyList<TenantTerminationOwnerWorkItem>> prepared =
            planner.PrepareWorkItems(process, Now.AddMinutes(3));

        Assert.True(prepared.IsSuccess);
        Assert.Equal(
            ["root-a", "root-b", "product", "task-runtime", "workspaces"],
            prepared.Value.Select(item => item.OwnerKey));
        Assert.All(prepared.Value, item =>
        {
            Assert.Equal(process.Id, item.ProcessId);
            Assert.Equal(process.OperationRevision, item.OperationRevision);
            Assert.Equal(process.PolicyEvidenceSha256, item.PolicyEvidenceSha256);
            Assert.Equal(TenantTerminationOwnerWorkState.Prepared, item.State);
        });

        IReadOnlyList<TenantTerminationPlannedDispatch> ready =
            planner.FindReadyDispatches(process, prepared.Value).Value;
        Assert.Equal(
            ["root-a", "root-b"],
            ready.Select(dispatch => dispatch.OwnerKey));
        Assert.All(ready, dispatch => Assert.Equal(
            TenantTerminationExecutionBoundary.TenantScopedTask,
            dispatch.ExecutionBoundary));

        Complete(prepared.Value.Single(item => item.OwnerKey == "root-a"), 4);
        ready = planner.FindReadyDispatches(process, prepared.Value).Value;
        Assert.Equal(["root-b"], ready.Select(item => item.OwnerKey));

        Complete(prepared.Value.Single(item => item.OwnerKey == "root-b"), 7);
        ready = planner.FindReadyDispatches(process, prepared.Value).Value;
        TenantTerminationPlannedDispatch product = Assert.Single(ready);
        Assert.Equal("product", product.OwnerKey);
        Assert.Equal(1, product.DispatchSequence);

        Complete(prepared.Value.Single(item => item.OwnerKey == "product"), 10);
        ready = planner.FindReadyDispatches(process, prepared.Value).Value;
        TenantTerminationPlannedDispatch taskRuntime = Assert.Single(ready);
        Assert.Equal("task-runtime", taskRuntime.OwnerKey);
        Assert.Equal(
            TenantTerminationExecutionBoundary.GlobalControlTask,
            taskRuntime.ExecutionBoundary);

        Complete(
            prepared.Value.Single(item => item.OwnerKey == "task-runtime"),
            13);
        ready = planner.FindReadyDispatches(process, prepared.Value).Value;
        TenantTerminationPlannedDispatch workspaces = Assert.Single(ready);
        Assert.Equal("workspaces", workspaces.OwnerKey);
        Assert.Equal(
            TenantTerminationExecutionBoundary.GlobalControlTask,
            workspaces.ExecutionBoundary);

        Assert.Equal(
            workspaces.TaskRunId,
            TenantTerminationExecutionIdentity.CreateTaskRunId(
                workspaces.WorkItemId,
                workspaces.DispatchSequence));
    }

    [Fact]
    public void Retry_required_releases_the_next_deterministic_dispatch()
    {
        TenantTerminationProcess process = CreateRunningDestroyProcess();
        TenantTerminationPhasePlanner planner = new([Stub("reservations")]);
        TenantTerminationOwnerWorkItem workItem = Assert.Single(
            planner.PrepareWorkItems(process, Now.AddMinutes(3)).Value);
        TenantTerminationPlannedDispatch first = Assert.Single(
            planner.FindReadyDispatches(process, [workItem]).Value);

        Assert.True(workItem.BeginProcessing(
            first.TaskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(4)).IsSuccess);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.RetryRequired,
            "reservations.more-work",
            affectedCount: 500,
            retainedMinimumCount: 0,
            remainingActiveCount: 1,
            holdReviewAtUtc: null,
            selectedProofRevision: null,
            resultingProofRevision: null,
            catalogVersion: workItem.CatalogVersion,
            workItem.CatalogSha256,
            first.TaskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(5)).IsSuccess);

        TenantTerminationPlannedDispatch continuation = Assert.Single(
            planner.FindReadyDispatches(process, [workItem]).Value);
        Assert.Equal(2, continuation.DispatchSequence);
        Assert.NotEqual(first.TaskRunId, continuation.TaskRunId);
        Assert.Equal(first.WorkItemId, continuation.WorkItemId);
    }

    [Fact]
    public void Missing_or_foreign_work_item_fails_closed()
    {
        TenantTerminationProcess process = CreateRunningDestroyProcess();
        TenantTerminationPhasePlanner planner = new(
            [Stub("reservations"), Stub("guests", ["reservations"])]);
        IReadOnlyList<TenantTerminationOwnerWorkItem> prepared =
            planner.PrepareWorkItems(process, Now.AddMinutes(3)).Value;

        Result<IReadOnlyList<TenantTerminationPlannedDispatch>> missing =
            planner.FindReadyDispatches(process, [prepared[0]]);
        Assert.True(missing.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid,
            missing.Error);

        TenantTerminationProcess foreignProcess = CreateRunningDestroyProcess();
        TenantTerminationOwnerWorkItem foreign = Assert.Single(
            new TenantTerminationPhasePlanner([Stub("guests")])
                .PrepareWorkItems(
                    foreignProcess,
                    Now.AddMinutes(3))
                .Value);
        Assert.True(planner.FindReadyDispatches(
            process,
            [prepared[0], foreign]).IsFailure);
    }

    [Fact]
    public void Central_verify_phase_has_no_owner_execution_plan()
    {
        TenantTerminationProcess process = CreateRunningDestroyProcess();
        Assert.True(process.CompletePhase(
            TenantTerminationProcessPhase.Destroy,
            process.OperationRevision,
            process.Version,
            "executor",
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Verify,
            process.Version,
            "executor",
            Now.AddMinutes(4)).IsSuccess);

        Result<IReadOnlyList<TenantTerminationPlannedOwnerWork>> result =
            new TenantTerminationPhasePlanner([Stub("reservations")])
                .Plan(process);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid,
            result.Error);
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

    private static StubContributor Stub(
        string ownerKey,
        IReadOnlyCollection<string>? dependencies = null,
        TenantTerminationExecutionBoundary executionBoundary =
            TenantTerminationExecutionBoundary.TenantScopedTask) =>
        new(new(
            ownerKey,
            TenantTerminationContract.CurrentVersion,
            [
                new(
                    TenantTerminationContributionPhase.Destroy,
                    dependencies ?? [],
                    executionBoundary)
            ],
            MandatoryForProduction: true,
            CatalogVersion: 2,
            CatalogSha256: Digest));

    private static void Complete(
        TenantTerminationOwnerWorkItem workItem,
        int minute)
    {
        Guid taskRunId = TenantTerminationExecutionIdentity.CreateTaskRunId(
            workItem.Id,
            workItem.AttemptCount + 1);
        Assert.True(workItem.BeginProcessing(
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(minute)).IsSuccess);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            $"{workItem.OwnerKey}.completed",
            affectedCount: 0,
            retainedMinimumCount: 0,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision: 0,
            resultingProofRevision: 0,
            catalogVersion: workItem.CatalogVersion,
            workItem.CatalogSha256,
            taskRunId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(minute + 1)).IsSuccess);
    }

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
}
