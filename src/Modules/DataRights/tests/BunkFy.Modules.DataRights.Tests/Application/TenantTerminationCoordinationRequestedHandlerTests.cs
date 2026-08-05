namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Tasks;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationCoordinationRequestedHandlerTests
{
    private static readonly string Digest = new('b', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 21, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pending_phase_dispatches_the_exact_transactional_start()
    {
        TenantTerminationProcess process = PrepareProcess();
        RecordingDispatcher dispatcher = new(command => command switch
        {
            BeginTenantTerminationPhaseCommand begin => Result.Success(
                new TenantTerminationPhaseStart(
                    begin.ProcessId,
                    begin.Phase,
                    begin.ExpectedProcessVersion + 1,
                    process.OperationRevision + 1,
                    [])),
            _ => throw new NotSupportedException()
        });
        RecordingScheduler scheduler = new();
        TenantTerminationCoordinationRequestedHandler handler = new(
            new StubRepository(process),
            dispatcher,
            scheduler);

        await handler.HandleAsync(
            Event(
                process,
                TenantTerminationCoordinationAction.BeginOwnerPhase,
                TenantTerminationContributionPhase.Freeze),
            CancellationToken.None);

        BeginTenantTerminationPhaseCommand command = Assert.IsType<
            BeginTenantTerminationPhaseCommand>(Assert.Single(
                dispatcher.Commands));
        Assert.Equal(process.Id, command.ProcessId);
        Assert.Equal(process.Phase, command.Phase);
        Assert.Equal(process.Version, command.ExpectedProcessVersion);
        Assert.Equal(
            TenantTerminationCoordination.ExecutorActorId,
            command.ActorId);
        Assert.Empty(scheduler.Dispatches);
    }

    [Fact]
    public async Task Running_phase_schedules_only_reconciled_ready_work()
    {
        TenantTerminationProcess process = PrepareProcess();
        Assert.True(process.BeginPhase(
            process.Phase,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(1)).IsSuccess);
        Guid taskRunId = Guid.NewGuid();
        TenantTerminationPlannedDispatch ready = new(
            process.Id,
            Guid.NewGuid(),
            process.OperationRevision,
            TenantTerminationContributionPhase.Freeze,
            "workspaces",
            TenantTerminationExecutionBoundary.TenantScopedTask,
            DispatchSequence: 1,
            taskRunId,
            TenantTerminationExecutionIdentity.CreateTaskDeduplicationKey(
                taskRunId));
        RecordingDispatcher dispatcher = new(command => command switch
        {
            ReconcileTenantTerminationPhaseCommand reconcile =>
                Result.Success(new TenantTerminationPhaseReconciliation(
                    reconcile.ProcessId,
                    reconcile.Phase,
                    TenantTerminationProcessStatus.Running,
                    reconcile.ExpectedProcessVersion,
                    reconcile.OperationRevision,
                    [ready],
                    ExportArtifactRequired: false)),
            _ => throw new NotSupportedException()
        });
        RecordingScheduler scheduler = new();
        TenantTerminationCoordinationRequestedHandler handler = new(
            new StubRepository(process),
            dispatcher,
            scheduler);

        await handler.HandleAsync(
            Event(
                process,
                TenantTerminationCoordinationAction.ReconcileOwnerPhase,
                TenantTerminationContributionPhase.Freeze),
            CancellationToken.None);

        ReconcileTenantTerminationPhaseCommand command = Assert.IsType<
            ReconcileTenantTerminationPhaseCommand>(Assert.Single(
                dispatcher.Commands));
        Assert.Equal(process.Version, command.ExpectedProcessVersion);
        Assert.Equal(process.OperationRevision, command.OperationRevision);
        Assert.Equal(process.ScopeId, scheduler.TenantId);
        Assert.Same(ready, Assert.Single(scheduler.Dispatches));
    }

    [Fact]
    public async Task Superseded_signal_is_acknowledged_without_new_work()
    {
        TenantTerminationProcess process = PrepareProcess();
        long staleVersion = process.Version;
        long staleOperationRevision = process.OperationRevision;
        Assert.True(process.BeginPhase(
            process.Phase,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(1)).IsSuccess);
        RecordingDispatcher dispatcher = new(_ =>
            throw new InvalidOperationException("Must not dispatch."));
        RecordingScheduler scheduler = new();
        TenantTerminationCoordinationRequestedHandler handler = new(
            new StubRepository(process),
            dispatcher,
            scheduler);
        TenantTerminationCoordinationRequestedIntegrationEvent stale = new(
            Guid.NewGuid(),
            process.ScopeId,
            Now,
            process.Id,
            staleVersion,
            staleOperationRevision,
            TenantTerminationCoordinationAction.BeginOwnerPhase,
            TenantTerminationContributionPhase.Freeze);

        await handler.HandleAsync(stale, CancellationToken.None);

        Assert.Empty(dispatcher.Commands);
        Assert.Empty(scheduler.Dispatches);
    }

    [Fact]
    public async Task Completed_export_schedules_the_deterministic_artifact_task()
    {
        TenantTerminationProcess process = PrepareExportingProcess();
        RecordingDispatcher dispatcher = new(command => command switch
        {
            ReconcileTenantTerminationPhaseCommand reconcile =>
                Result.Success(new TenantTerminationPhaseReconciliation(
                    reconcile.ProcessId,
                    reconcile.Phase,
                    TenantTerminationProcessStatus.Running,
                    reconcile.ExpectedProcessVersion,
                    reconcile.OperationRevision,
                    [],
                    ExportArtifactRequired: true)),
            _ => throw new NotSupportedException()
        });
        RecordingScheduler scheduler = new();
        TenantTerminationCoordinationRequestedHandler handler = new(
            new StubRepository(process),
            dispatcher,
            scheduler);

        await handler.HandleAsync(
            Event(
                process,
                TenantTerminationCoordinationAction.ReconcileOwnerPhase,
                TenantTerminationContributionPhase.Export),
            CancellationToken.None);

        Assert.Empty(scheduler.Dispatches);
        Assert.Equal(process.ScopeId, scheduler.TenantId);
        (Guid processId, long operationRevision) =
            Assert.Single(scheduler.ExportArtifacts);
        Assert.Equal(process.Id, processId);
        Assert.Equal(process.OperationRevision, operationRevision);
    }

    [Fact]
    public async Task Pending_verify_schedules_the_deterministic_verifier()
    {
        TenantTerminationProcess process = PrepareVerifyingProcess();
        long verificationRevision = process.OperationRevision + 1;
        RecordingDispatcher dispatcher = new(command => command switch
        {
            BeginTenantTerminationVerificationCommand begin => Result.Success(
                new TenantTerminationVerificationPhaseStart(
                    begin.ProcessId,
                    begin.ExpectedProcessVersion + 1,
                    process.DestroyCompletedOperationRevision!.Value,
                    verificationRevision,
                    TenantTerminationExecutionIdentity
                        .CreateVerificationTaskRunId(
                            process.Id,
                            verificationRevision))),
            _ => throw new NotSupportedException()
        });
        RecordingScheduler scheduler = new();
        TenantTerminationCoordinationRequestedHandler handler = new(
            new StubRepository(process),
            dispatcher,
            scheduler);

        await handler.HandleAsync(
            Event(
                process,
                TenantTerminationCoordinationAction.Verify,
                TenantTerminationContributionPhase.Unknown),
            CancellationToken.None);

        BeginTenantTerminationVerificationCommand command = Assert.IsType<
            BeginTenantTerminationVerificationCommand>(Assert.Single(
                dispatcher.Commands));
        Assert.Equal(process.Id, command.ProcessId);
        Assert.Equal(process.Version, command.ExpectedProcessVersion);
        Assert.Equal(process.ScopeId, scheduler.TenantId);
        Assert.Equal(
            (process.Id, verificationRevision),
            Assert.Single(scheduler.Verifications));
    }

    [Fact]
    public async Task Future_or_cross_tenant_coordinates_fail_closed()
    {
        TenantTerminationProcess process = PrepareProcess();
        TenantTerminationCoordinationRequestedHandler handler = new(
            new StubRepository(process),
            new RecordingDispatcher(_ => throw new NotSupportedException()),
            new RecordingScheduler());
        TenantTerminationCoordinationRequestedIntegrationEvent future = new(
            Guid.NewGuid(),
            process.ScopeId,
            Now,
            process.Id,
            process.Version + 1,
            process.OperationRevision,
            TenantTerminationCoordinationAction.BeginOwnerPhase,
            TenantTerminationContributionPhase.Freeze);
        TenantTerminationCoordinationRequestedIntegrationEvent foreign = new(
            Guid.NewGuid(),
            "tenant-b",
            Now,
            process.Id,
            process.Version,
            process.OperationRevision,
            TenantTerminationCoordinationAction.BeginOwnerPhase,
            TenantTerminationContributionPhase.Freeze);

        InvalidOperationException futureFailure = await Assert.ThrowsAsync<
            InvalidOperationException>(() => handler.HandleAsync(
                future,
                CancellationToken.None));
        InvalidOperationException foreignFailure = await Assert.ThrowsAsync<
            InvalidOperationException>(() => handler.HandleAsync(
                foreign,
                CancellationToken.None));

        Assert.Equal(
            "DataRights.TenantTerminationCoordinationCoordinatesInvalid",
            futureFailure.Message);
        Assert.Equal(futureFailure.Message, foreignFailure.Message);
    }

    private static TenantTerminationCoordinationRequestedIntegrationEvent Event(
        TenantTerminationProcess process,
        TenantTerminationCoordinationAction action,
        TenantTerminationContributionPhase phase) =>
        new(
            Guid.NewGuid(),
            process.ScopeId,
            Now.AddMinutes(2),
            process.Id,
            process.Version,
            process.OperationRevision,
            action,
            phase);

    private static TenantTerminationProcess PrepareProcess() =>
        TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 5,
            Guid.NewGuid(),
            exportRequested: false,
            Digest,
            "approver",
            Now,
            "creator",
            Now).Value;

    private static TenantTerminationProcess PrepareExportingProcess()
    {
        TenantTerminationProcess process = TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 5,
            Guid.NewGuid(),
            exportRequested: true,
            Digest,
            "approver",
            Now,
            "creator",
            Now).Value;
        _ = process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(1));
        _ = process.CompleteFreeze(
            process.OperationRevision,
            workspaceFenceRevision: 1,
            Digest,
            [new("workspaces", 1, 1, Digest)],
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(2));
        _ = process.BeginPhase(
            TenantTerminationProcessPhase.Export,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(3));
        return process;
    }

    private static TenantTerminationProcess PrepareVerifyingProcess()
    {
        TenantTerminationProcess process = PrepareProcess();
        _ = process.BeginPhase(
            TenantTerminationProcessPhase.Freeze,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(1));
        _ = process.CompleteFreeze(
            process.OperationRevision,
            workspaceFenceRevision: 1,
            Digest,
            [new("workspaces", 1, 1, Digest)],
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(2));
        _ = process.BeginPhase(
            TenantTerminationProcessPhase.Destroy,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(3));
        _ = process.CompletePhase(
            TenantTerminationProcessPhase.Destroy,
            process.OperationRevision,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(4));
        return process;
    }

    private sealed class RecordingDispatcher(Func<object, object> resultFactory)
        : IRequestDispatcher
    {
        public List<object> Commands { get; } = [];

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            this.Commands.Add(command);
            return Task.FromResult((Result<TResponse>)resultFactory(command));
        }

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingScheduler : ITenantTerminationTaskScheduler
    {
        public string? TenantId { get; private set; }
        public List<TenantTerminationPlannedDispatch> Dispatches { get; } = [];
        public List<(Guid ProcessId, long OperationRevision)> ExportArtifacts
        { get; } = [];
        public List<(Guid ProcessId, long OperationRevision)> Verifications
        { get; } = [];

        public Task EnqueueAsync(
            string tenantId,
            IReadOnlyCollection<TenantTerminationPlannedDispatch> dispatches,
            CancellationToken cancellationToken)
        {
            this.TenantId = tenantId;
            this.Dispatches.AddRange(dispatches);
            return Task.CompletedTask;
        }

        public Task EnqueueExportArtifactAsync(
            string tenantId,
            Guid processId,
            long operationRevision,
            CancellationToken cancellationToken)
        {
            this.TenantId = tenantId;
            this.ExportArtifacts.Add((processId, operationRevision));
            return Task.CompletedTask;
        }

        public Task EnqueueVerificationAsync(
            string tenantId,
            Guid processId,
            long operationRevision,
            CancellationToken cancellationToken)
        {
            this.TenantId = tenantId;
            this.Verifications.Add((processId, operationRevision));
            return Task.CompletedTask;
        }
    }

    private sealed class StubRepository(TenantTerminationProcess process)
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
            throw new NotSupportedException();

        public Task<IReadOnlyList<TenantTerminationOwnerWorkItem>>
            ListOwnerWorkItemsAsync(
                Guid processId,
                TenantTerminationOwnerPhase phase,
                long operationRevision,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
