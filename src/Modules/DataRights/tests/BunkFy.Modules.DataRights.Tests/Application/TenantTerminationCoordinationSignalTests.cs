namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Messaging;
using Gma.Framework.Runtime.Identity;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationCoordinationSignalTests
{
    private static readonly string Digest = new('a', 64);
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Pending_and_running_state_emit_exact_durable_coordinates()
    {
        TenantTerminationProcess process = PrepareProcess();
        RecordingOutbox outbox = new();
        Guid firstEventId = Guid.NewGuid();
        Guid secondEventId = Guid.NewGuid();
        TenantTerminationCoordinationSignal signal = new(
            new FixedOutboxRegistry(outbox),
            new SequenceIdGenerator(firstEventId, secondEventId));

        Assert.True(await signal.EnqueueAsync(
            process,
            Now.AddMinutes(1),
            CancellationToken.None));

        TenantTerminationCoordinationRequestedIntegrationEvent pending =
            Assert.IsType<
                TenantTerminationCoordinationRequestedIntegrationEvent>(
                    outbox.Events[0]);
        Assert.Equal(firstEventId, pending.EventId);
        Assert.Equal(process.Id, pending.ProcessId);
        Assert.Equal(process.Version, pending.ProcessVersion);
        Assert.Equal(process.OperationRevision, pending.OperationRevision);
        Assert.Equal(
            TenantTerminationCoordinationAction.BeginOwnerPhase,
            pending.Action);
        Assert.Equal(TenantTerminationContributionPhase.Freeze, pending.Phase);

        Assert.True(process.BeginPhase(
            process.Phase,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(2)).IsSuccess);
        Assert.True(await signal.EnqueueAsync(
            process,
            Now.AddMinutes(2),
            CancellationToken.None));

        TenantTerminationCoordinationRequestedIntegrationEvent running =
            Assert.IsType<
                TenantTerminationCoordinationRequestedIntegrationEvent>(
                    outbox.Events[1]);
        Assert.Equal(secondEventId, running.EventId);
        Assert.Equal(process.Version, running.ProcessVersion);
        Assert.Equal(process.OperationRevision, running.OperationRevision);
        Assert.Equal(
            TenantTerminationCoordinationAction.ReconcileOwnerPhase,
            running.Action);
        Assert.Equal(TenantTerminationContributionPhase.Freeze, running.Phase);
    }

    [Fact]
    public async Task Settled_state_does_not_create_polling_work()
    {
        TenantTerminationProcess process = PrepareProcess();
        Assert.True(process.BeginPhase(
            process.Phase,
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(1)).IsSuccess);
        Assert.True(process.RecordFailed(
            process.Phase,
            process.OperationRevision,
            "workspaces.failed",
            process.Version,
            TenantTerminationCoordination.ExecutorActorId,
            Now.AddMinutes(2)).IsSuccess);
        RecordingOutbox outbox = new();
        TenantTerminationCoordinationSignal signal = new(
            new FixedOutboxRegistry(outbox),
            new SequenceIdGenerator(Guid.NewGuid()));

        bool emitted = await signal.EnqueueAsync(
            process,
            Now.AddMinutes(2),
            CancellationToken.None);

        Assert.False(emitted);
        Assert.Empty(outbox.Events);
    }

    private static TenantTerminationProcess PrepareProcess() =>
        TenantTerminationProcess.Prepare(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            approvalRevision: 3,
            Guid.NewGuid(),
            exportRequested: false,
            Digest,
            "approver",
            Now,
            "creator",
            Now).Value;

    private sealed class RecordingOutbox : IOutboxWriter
    {
        public string ModuleName => DataRightsModuleMetadata.Name;
        public List<IIntegrationEvent> Events { get; } = [];

        public Task EnqueueAsync<TEvent>(
            TEvent integrationEvent,
            CancellationToken cancellationToken)
            where TEvent : IIntegrationEvent
        {
            this.Events.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedOutboxRegistry(IOutboxWriter outbox)
        : IOutboxWriterRegistry
    {
        public IOutboxWriter GetRequired(string moduleName)
        {
            Assert.Equal(DataRightsModuleMetadata.Name, moduleName);
            return outbox;
        }
    }

    private sealed class SequenceIdGenerator(params Guid[] ids) : IIdGenerator
    {
        private readonly Queue<Guid> remaining = new(ids);

        public Guid NewId() => this.remaining.Dequeue();
    }
}
