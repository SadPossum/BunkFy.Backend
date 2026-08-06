namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReconcileTenantTerminationPhaseCommandHandlerTests
{
    private const string Approver = "owner:approver";
    private const string Executor = "system:tenant-termination-executor";
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Completed_freeze_captures_fence_and_exact_export_catalog()
    {
        ITenantTerminationContributor[] contributors =
        [
            Stub(
                "workspaces",
                TenantTerminationContributionPhase.Freeze,
                TenantTerminationContributionPhase.Export),
            Stub("reservations", TenantTerminationContributionPhase.Export)
        ];
        Fixture fixture = CreateRunningFixture(
            contributors,
            TenantTerminationProcessPhase.Freeze,
            exportRequested: true);
        Complete(
            Assert.Single(fixture.WorkItems),
            selectedProofRevision: 4,
            resultingProofRevision: 5);

        Result<TenantTerminationPhaseReconciliation> result =
            await fixture.Handler.HandleAsync(
                Command(fixture.Process),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantTerminationProcessPhase.Export, fixture.Process.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Pending, fixture.Process.Status);
        Assert.Equal(5, fixture.Process.WorkspaceFenceRevision);
        Assert.Equal(
            ["reservations", "workspaces"],
            fixture.Process.FrozenExportOwners
                .OrderBy(owner => owner.Ordinal)
                .Select(owner => owner.OwnerKey)
                .ToArray());
        TenantTerminationFrozenRevision snapshot = new(
            fixture.Process.ScopeId,
            fixture.Process.Id,
            fixture.Process.CaseId,
            fixture.Process.ApprovalRevision,
            fixture.Process.FreezeOperationRevision.GetValueOrDefault(),
            fixture.Process.TerminationEpoch,
            fixture.Process.WorkspaceFenceRevision.GetValueOrDefault(),
            fixture.Process.PolicyEvidenceSha256,
            fixture.Process.FrozenAtUtc.GetValueOrDefault(),
            fixture.Process.FrozenExportOwners.Select(owner =>
                new TenantTerminationExportOwnerCatalogEntry(
                    owner.OwnerKey,
                    owner.ContractVersion,
                    owner.CatalogVersion,
                    owner.CatalogSha256))
                .ToArray());
        Assert.Equal(
            TenantTerminationExportFragmentAssembler
                .ComputeFrozenRevisionSha256(snapshot),
            fixture.Process.FrozenRevisionSha256);
        Assert.Empty(result.Value.ReadyDispatches);
        TenantTerminationSignalCapture wake = Assert.Single(
            fixture.Signal.Captures);
        Assert.Equal(TenantTerminationProcessPhase.Export, wake.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Pending, wake.Status);
    }

    [Fact]
    public async Task Freeze_rejects_an_ambiguous_fence_authority()
    {
        ITenantTerminationContributor[] contributors =
        [
            Stub(
                "workspaces",
                TenantTerminationContributionPhase.Freeze,
                TenantTerminationContributionPhase.Export),
            Stub(
                "another-fence",
                TenantTerminationContributionPhase.Freeze,
                TenantTerminationContributionPhase.Export)
        ];
        Fixture fixture = CreateRunningFixture(
            contributors,
            TenantTerminationProcessPhase.Freeze,
            exportRequested: false);
        foreach (TenantTerminationOwnerWorkItem workItem in fixture.WorkItems)
        {
            Complete(
                workItem,
                selectedProofRevision: 0,
                resultingProofRevision: 1);
        }

        Result<TenantTerminationPhaseReconciliation> result =
            await fixture.Handler.HandleAsync(
                Command(fixture.Process),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors.TenantTerminationExecutionStateInvalid,
            result.Error);
        Assert.Equal(
            TenantTerminationProcessStatus.Running,
            fixture.Process.Status);
        Assert.Null(fixture.Process.FrozenRevisionSha256);
    }

    [Fact]
    public async Task Blocked_owner_settles_the_process_without_dispatching_more_work()
    {
        ITenantTerminationContributor[] contributors =
        [Stub("workspaces", TenantTerminationContributionPhase.Freeze)];
        Fixture fixture = CreateRunningFixture(
            contributors,
            TenantTerminationProcessPhase.Freeze,
            exportRequested: false);
        Block(Assert.Single(fixture.WorkItems), Now.AddDays(1));

        Result<TenantTerminationPhaseReconciliation> result =
            await fixture.Handler.HandleAsync(
                Command(fixture.Process),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            TenantTerminationProcessStatus.Blocked,
            fixture.Process.Status);
        Assert.Equal(
            TenantTerminationPhaseEvaluator.BlockedOutcomeCode,
            fixture.Process.OutcomeCode);
        Assert.Equal(Now.AddDays(1), fixture.Process.HoldReviewAtUtc);
        Assert.Empty(result.Value.ReadyDispatches);
    }

    [Fact]
    public async Task Running_phase_returns_only_the_deterministic_ready_work()
    {
        ITenantTerminationContributor[] contributors =
        [
            Stub("workspaces", TenantTerminationContributionPhase.Freeze),
            Stub(
                "reservations",
                ["workspaces"],
                TenantTerminationContributionPhase.Freeze)
        ];
        Fixture fixture = CreateRunningFixture(
            contributors,
            TenantTerminationProcessPhase.Freeze,
            exportRequested: false);
        long version = fixture.Process.Version;

        Result<TenantTerminationPhaseReconciliation> result =
            await fixture.Handler.HandleAsync(
                Command(fixture.Process),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(version, fixture.Process.Version);
        Assert.Equal(
            "workspaces",
            Assert.Single(result.Value.ReadyDispatches).OwnerKey);
        Assert.Empty(fixture.Signal.Captures);
    }

    [Fact]
    public async Task Completed_destroy_advances_to_central_verification()
    {
        TenantTerminationProcess process = PrepareProcess(
            exportRequested: false);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Executor,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(process.CompleteFreeze(
            process.OperationRevision,
            workspaceFenceRevision: 1,
            Digest,
            [new("workspaces", 1, 1, Digest)],
            process.Version,
            Executor,
            Now.AddMinutes(2)).IsSuccess);
        ITenantTerminationContributor[] contributors =
        [Stub("workspaces", TenantTerminationContributionPhase.Destroy)];
        Fixture fixture = CreateRunningFixture(
            contributors,
            process,
            TenantTerminationProcessPhase.Destroy,
            Now.AddMinutes(3));
        Complete(
            Assert.Single(fixture.WorkItems),
            selectedProofRevision: 1,
            resultingProofRevision: 2);

        Result<TenantTerminationPhaseReconciliation> result =
            await fixture.Handler.HandleAsync(
                Command(process),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantTerminationProcessPhase.Verify, process.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Pending, process.Status);
        TenantTerminationSignalCapture wake = Assert.Single(
            fixture.Signal.Captures);
        Assert.Equal(TenantTerminationProcessPhase.Verify, wake.Phase);
    }

    [Fact]
    public async Task Completed_export_waits_for_the_protected_artifact()
    {
        ITenantTerminationContributor[] contributors =
        [Stub("workspaces", TenantTerminationContributionPhase.Export)];
        TenantTerminationProcess process = PrepareProcess(
            exportRequested: true);
        Assert.True(process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            Executor,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(process.CompleteFreeze(
            process.OperationRevision,
            workspaceFenceRevision: 1,
            Digest,
            [new("workspaces", 1, 2, Digest)],
            process.Version,
            Executor,
            Now.AddMinutes(2)).IsSuccess);
        Fixture fixture = CreateRunningFixture(
            contributors,
            process,
            TenantTerminationProcessPhase.Export,
            Now.AddMinutes(3));
        Complete(
            Assert.Single(fixture.WorkItems),
            selectedProofRevision: 7,
            resultingProofRevision: 7);
        long version = process.Version;

        Result<TenantTerminationPhaseReconciliation> result =
            await fixture.Handler.HandleAsync(
                Command(process),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ExportArtifactRequired);
        Assert.Equal(version, process.Version);
        Assert.Equal(TenantTerminationProcessPhase.Export, process.Phase);
        Assert.Equal(TenantTerminationProcessStatus.Running, process.Status);
        Assert.Empty(result.Value.ReadyDispatches);
        Assert.Empty(fixture.Signal.Captures);
    }

    private static Fixture CreateRunningFixture(
        IReadOnlyCollection<ITenantTerminationContributor> contributors,
        TenantTerminationProcessPhase phase,
        bool exportRequested)
    {
        TenantTerminationProcess process = PrepareProcess(exportRequested);
        return CreateRunningFixture(
            contributors,
            process,
            phase,
            Now.AddMinutes(1));
    }

    private static Fixture CreateRunningFixture(
        IReadOnlyCollection<ITenantTerminationContributor> contributors,
        TenantTerminationProcess process,
        TenantTerminationProcessPhase phase,
        DateTimeOffset startedAtUtc)
    {
        Assert.True(process.BeginPhase(
            phase,
            process.Version,
            Executor,
            startedAtUtc).IsSuccess);
        TenantTerminationPhasePlanner planner = new(contributors);
        List<TenantTerminationOwnerWorkItem> workItems =
            [.. planner.PrepareWorkItems(process, startedAtUtc).Value];
        StubRepository repository = new(process, workItems);
        RecordingTenantTerminationCoordinationSignal signal = new();
        return new(
            process,
            workItems,
            signal,
            new ReconcileTenantTerminationPhaseCommandHandler(
                repository,
                DataRightsMutationTestSupport.TenantTermination(
                    repository,
                    new StubCaseRepository()),
                new TenantTerminationPhaseEvaluator(planner),
                contributors,
                signal,
                new FixedClock(Now.AddMinutes(10))));
    }

    private static ReconcileTenantTerminationPhaseCommand Command(
        TenantTerminationProcess process) =>
        new(
            process.Id,
            process.Phase,
            process.OperationRevision,
            process.Version,
            Executor);

    private static TenantTerminationProcess PrepareProcess(
        bool exportRequested) =>
        TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 4,
            Guid.NewGuid(),
            exportRequested,
            Digest,
            Approver,
            Now,
            "creator",
            Now).Value;

    private sealed class StubCaseRepository
        : ITenantTerminationCaseRepository
    {
        public Task AddAsync(
            DataRightsCase dataRightsCase,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DataRightsCase?> GetAsync(
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<DataRightsCase?>(null);

        public Task<DataRightsCase?> GetActiveAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static void Complete(
        TenantTerminationOwnerWorkItem workItem,
        long selectedProofRevision,
        long resultingProofRevision)
    {
        Begin(workItem);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.Completed,
            $"{workItem.OwnerKey}.completed",
            affectedCount: 1,
            retainedMinimumCount: 0,
            remainingActiveCount: 0,
            holdReviewAtUtc: null,
            selectedProofRevision,
            resultingProofRevision,
            workItem.CatalogVersion,
            workItem.CatalogSha256,
            workItem.TaskRunId!.Value,
            workItem.LastTaskAttempt,
            workItem.Version,
            Now.AddMinutes(4)).IsSuccess);
    }

    private static void Block(
        TenantTerminationOwnerWorkItem workItem,
        DateTimeOffset? reviewAtUtc)
    {
        Begin(workItem);
        Assert.True(workItem.RecordResult(
            TenantTerminationOwnerWorkState.Blocked,
            $"{workItem.OwnerKey}.blocked",
            affectedCount: 0,
            retainedMinimumCount: 0,
            remainingActiveCount: 1,
            reviewAtUtc,
            selectedProofRevision: null,
            resultingProofRevision: null,
            workItem.CatalogVersion,
            workItem.CatalogSha256,
            workItem.TaskRunId!.Value,
            workItem.LastTaskAttempt,
            workItem.Version,
            Now.AddMinutes(4)).IsSuccess);
    }

    private static void Begin(TenantTerminationOwnerWorkItem workItem)
    {
        Guid runId = Guid.NewGuid();
        Assert.True(workItem.BeginProcessing(
            runId,
            taskAttempt: 1,
            workItem.Version,
            Now.AddMinutes(3)).IsSuccess);
    }

    private static StubContributor Stub(
        string ownerKey,
        params TenantTerminationContributionPhase[] phases) =>
        Stub(ownerKey, [], phases);

    private static StubContributor Stub(
        string ownerKey,
        IReadOnlyCollection<string> dependencies,
        params TenantTerminationContributionPhase[] phases) =>
        new(new(
            ownerKey,
            TenantTerminationContract.CurrentVersion,
            phases.Select(phase =>
                new TenantTerminationContributorPhasePlan(
                    phase,
                    dependencies))
                .ToArray(),
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

    private sealed class StubRepository(
        TenantTerminationProcess process,
        IReadOnlyList<TenantTerminationOwnerWorkItem> workItems)
        : ITenantTerminationRepository
    {
        public Task AddProcessAsync(
            TenantTerminationProcess candidate,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddOwnerWorkItemAsync(
            TenantTerminationOwnerWorkItem workItem,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TenantTerminationProcess?> GetProcessAsync(
            Guid processId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationProcess?>(
                process.Id == processId ? process : null);

        public Task<TenantTerminationProcess?> GetActiveProcessAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationProcess?>(process);

        public Task<TenantTerminationProcess?>
            GetProcessByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult<TenantTerminationProcess?>(
                process.IdempotencyKey == idempotencyKey ? process : null);

        public Task<TenantTerminationOwnerWorkItem?> GetOwnerWorkItemAsync(
            Guid processId,
            TenantTerminationOwnerPhase phase,
            string ownerKey,
            long operationRevision,
            CancellationToken cancellationToken) =>
            Task.FromResult(workItems.SingleOrDefault(item =>
                item.ProcessId == processId &&
                item.Phase == phase &&
                item.OwnerKey == ownerKey &&
                item.OperationRevision == operationRevision));

        public Task<IReadOnlyList<TenantTerminationOwnerWorkItem>>
            ListOwnerWorkItemsAsync(
                Guid processId,
                TenantTerminationOwnerPhase phase,
                long operationRevision,
                CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TenantTerminationOwnerWorkItem>>(
                workItems.Where(item =>
                    item.ProcessId == processId &&
                    item.Phase == phase &&
                    item.OperationRevision == operationRevision)
                .OrderBy(item => item.OwnerKey, StringComparer.Ordinal)
                .ToArray());
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }

    private sealed record Fixture(
        TenantTerminationProcess Process,
        List<TenantTerminationOwnerWorkItem> WorkItems,
        RecordingTenantTerminationCoordinationSignal Signal,
        ReconcileTenantTerminationPhaseCommandHandler Handler);
}
