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
public sealed class BeginTenantTerminationPhaseCommandHandlerTests
{
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pending_phase_and_complete_owner_set_start_atomically()
    {
        TenantTerminationProcess process = PrepareProcess();
        StubRepository repository = new(process);
        RecordingTenantTerminationCoordinationSignal signal = new();
        BeginTenantTerminationPhaseCommandHandler handler = Handler(
            repository,
            [
                Stub("workspaces"),
                Stub("ingestion"),
                Stub("reservations", ["workspaces", "ingestion"])
            ],
            signal);
        BeginTenantTerminationPhaseCommand command = new(
            process.Id,
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            "executor");

        Result<TenantTerminationPhaseStart> result = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TenantTerminationProcessStatus.Running, process.Status);
        Assert.Equal(process.ApprovalRevision + 1, process.OperationRevision);
        Assert.Equal(3, repository.Added.Count);
        TenantTerminationSignalCapture wake = Assert.Single(signal.Captures);
        Assert.Equal(process.Version, wake.ProcessVersion);
        Assert.Equal(process.OperationRevision, wake.OperationRevision);
        Assert.Equal(TenantTerminationProcessStatus.Running, wake.Status);
        Assert.Equal(
            ["ingestion", "workspaces"],
            result.Value.ReadyDispatches.Select(item => item.OwnerKey));
        Assert.All(repository.Added, workItem => Assert.Equal(
            TenantTerminationExecutionIdentity.CreateWorkItemId(
                process.Id,
                process.OperationRevision,
                TenantTerminationOwnerPhase.Freeze,
                workItem.OwnerKey),
            workItem.Id));
    }

    [Fact]
    public async Task Exact_phase_start_replay_reuses_the_durable_work_set()
    {
        TenantTerminationProcess process = PrepareProcess();
        StubRepository repository = new(process);
        RecordingTenantTerminationCoordinationSignal signal = new();
        BeginTenantTerminationPhaseCommandHandler handler = Handler(
            repository,
            [Stub("workspaces"), Stub("ingestion")],
            signal);
        BeginTenantTerminationPhaseCommand command = new(
            process.Id,
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            "executor");

        Result<TenantTerminationPhaseStart> first = await handler.HandleAsync(
            command,
            CancellationToken.None);
        Result<TenantTerminationPhaseStart> replay = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(2, repository.Added.Count);
        Assert.Single(signal.Captures);
        Assert.Equal(
            first.Value.ReadyDispatches.Select(item => item.TaskRunId),
            replay.Value.ReadyDispatches.Select(item => item.TaskRunId));
    }

    [Fact]
    public async Task Replay_with_a_different_actor_or_incomplete_work_set_fails_closed()
    {
        TenantTerminationProcess process = PrepareProcess();
        StubRepository repository = new(process);
        BeginTenantTerminationPhaseCommandHandler handler = Handler(
            repository,
            [Stub("workspaces"), Stub("ingestion")]);
        long selectedVersion = process.Version;
        Assert.True((await handler.HandleAsync(
            new(
                process.Id,
                TenantTerminationProcessPhase.Freeze,
                selectedVersion,
                "executor"),
            CancellationToken.None)).IsSuccess);

        Result<TenantTerminationPhaseStart> wrongActor =
            await handler.HandleAsync(
                new(
                    process.Id,
                    TenantTerminationProcessPhase.Freeze,
                    selectedVersion,
                    "another-executor"),
                CancellationToken.None);
        Assert.True(wrongActor.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid,
            wrongActor.Error);

        repository.Added.RemoveAt(0);
        Result<TenantTerminationPhaseStart> incomplete =
            await handler.HandleAsync(
                new(
                    process.Id,
                    TenantTerminationProcessPhase.Freeze,
                    selectedVersion,
                    "executor"),
                CancellationToken.None);
        Assert.True(incomplete.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors
                .TenantTerminationExecutionStateInvalid,
            incomplete.Error);
    }

    [Fact]
    public async Task Missing_process_is_reported_without_preparing_work()
    {
        StubRepository repository = new(process: null);
        TenantTerminationProcess expected = PrepareProcess();

        Result<TenantTerminationPhaseStart> result = await Handler(
                repository,
                [Stub("workspaces")])
            .HandleAsync(
                new(
                    expected.Id,
                    TenantTerminationProcessPhase.Freeze,
                    expected.Version,
                    "executor"),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors.TenantTerminationProcessNotFound,
            result.Error);
        Assert.Empty(repository.Added);
    }

    private static BeginTenantTerminationPhaseCommandHandler Handler(
        StubRepository repository,
        IReadOnlyCollection<ITenantTerminationContributor> contributors,
        RecordingTenantTerminationCoordinationSignal? signal = null) =>
        new(
            repository,
            DataRightsMutationTestSupport.TenantTermination(repository),
            new TenantTerminationPhasePlanner(contributors),
            signal ?? new RecordingTenantTerminationCoordinationSignal(),
            new TestClock(Now.AddMinutes(1)));

    private static TenantTerminationProcess PrepareProcess() =>
        TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 7,
            Guid.NewGuid(),
            exportRequested: false,
            Digest,
            "approver",
            Now,
            "creator",
            Now).Value;

    private static StubContributor Stub(
        string ownerKey,
        IReadOnlyCollection<string>? dependencies = null) =>
        new(new(
            ownerKey,
            TenantTerminationContract.CurrentVersion,
            [
                new(
                    TenantTerminationContributionPhase.Freeze,
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

    private sealed class StubRepository(TenantTerminationProcess? process)
        : ITenantTerminationRepository
    {
        public List<TenantTerminationOwnerWorkItem> Added { get; } = [];

        public Task AddProcessAsync(
            TenantTerminationProcess process,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddOwnerWorkItemAsync(
            TenantTerminationOwnerWorkItem workItem,
            CancellationToken cancellationToken)
        {
            this.Added.Add(workItem);
            return Task.CompletedTask;
        }

        public Task<TenantTerminationProcess?> GetProcessAsync(
            Guid processId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                process?.Id == processId
                    ? process
                    : null);

        public Task<TenantTerminationProcess?> GetActiveProcessAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(process);

        public Task<TenantTerminationProcess?>
            GetProcessByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                process?.IdempotencyKey == idempotencyKey
                    ? process
                    : null);

        public Task<TenantTerminationOwnerWorkItem?> GetOwnerWorkItemAsync(
            Guid processId,
            TenantTerminationOwnerPhase phase,
            string ownerKey,
            long operationRevision,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Added.SingleOrDefault(item =>
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
                this.Added
                    .Where(item =>
                        item.ProcessId == processId &&
                        item.Phase == phase &&
                        item.OperationRevision == operationRevision)
                    .OrderBy(item => item.OwnerKey, StringComparer.Ordinal)
                    .ToArray());
    }

    private sealed class TestClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
