namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Adapters;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Runs;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RemoteAdapterLeaseHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CredentialId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkerId = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Claim_is_idempotent_for_one_nonce_and_expiry_takeover_increments_epoch()
    {
        AdapterConnection connection = CreateConnection();
        FakeRunRepository runs = new();
        MutableClock clock = new(Now);
        QueueIds ids = new(
            Guid.Parse("b0000000-0000-0000-0000-000000000001"),
            Guid.Parse("b0000000-0000-0000-0000-000000000002"),
            Guid.Parse("b0000000-0000-0000-0000-000000000003"),
            Guid.Parse("b0000000-0000-0000-0000-000000000004"));
        ClaimRemoteAdapterLeaseCommandHandler handler = new(
            new FakeConnectionRepository(connection),
            new ActivePropertyProjection(),
            runs,
            new DescriptorRegistry(),
            new TestScope(),
            clock,
            ids);
        Guid firstClaimId = Guid.NewGuid();
        AdapterRemoteLeaseClaimRequest firstRequest = new(
            firstClaimId, WorkerId, "fake.http", 1, 1, RequestedLeaseSeconds: 60);

        var first = await handler.HandleAsync(
            new(connection.Id, CredentialId, firstRequest), CancellationToken.None);
        clock.UtcNow = Now.AddSeconds(10);
        var retry = await handler.HandleAsync(
            new(connection.Id, CredentialId, firstRequest with { RequestedLeaseSeconds = 120 }),
            CancellationToken.None);
        var overlapping = await handler.HandleAsync(
            new(connection.Id, CredentialId, firstRequest with { ClaimId = Guid.NewGuid() }),
            CancellationToken.None);
        clock.UtcNow = Now.AddMinutes(3);
        var takeover = await handler.HandleAsync(
            new(connection.Id, CredentialId, firstRequest with { ClaimId = Guid.NewGuid() }),
            CancellationToken.None);

        Assert.True(first.IsSuccess, first.Error.Code);
        Assert.Equal(first.Value.Assignment.RunId, retry.Value.Assignment.RunId);
        Assert.Equal(first.Value.Assignment.LeaseId, retry.Value.Assignment.LeaseId);
        Assert.Equal(Now.AddSeconds(130), retry.Value.Assignment.LeaseExpiresAtUtc);
        Assert.Equal(40, retry.Value.RenewAfterSeconds);
        Assert.Equal(IngestionApplicationErrors.RemoteLeaseUnavailable, overlapping.Error);
        Assert.True(takeover.IsSuccess, takeover.Error.Code);
        Assert.Equal(2, takeover.Value.LeaseEpoch);
        Assert.Equal(IngestionRunState.Failed, runs.Items[0].State);
        Assert.Equal(IngestionRunState.Running, runs.Items[1].State);
    }

    [Fact]
    public async Task Renew_and_completion_require_exact_proof_and_completion_is_idempotent()
    {
        AdapterConnection connection = CreateConnection();
        FakeRunRepository runs = new();
        MutableClock clock = new(Now);
        ClaimRemoteAdapterLeaseCommandHandler claimHandler = new(
            new FakeConnectionRepository(connection),
            new ActivePropertyProjection(),
            runs,
            new DescriptorRegistry(),
            new TestScope(),
            clock,
            new QueueIds(Guid.NewGuid(), Guid.NewGuid()));
        var claim = await claimHandler.HandleAsync(new(
            connection.Id,
            CredentialId,
            new(Guid.NewGuid(), WorkerId, "fake.http", 1, 1, 60)), CancellationToken.None);
        AdapterRemoteLeaseProof proof = new(
            claim.Value.Assignment.RunId,
            claim.Value.Assignment.LeaseId,
            claim.Value.LeaseEpoch,
            WorkerId);
        RenewRemoteAdapterLeaseCommandHandler renewHandler = new(
            new FakeConnectionRepository(connection), runs, clock);

        var wrong = await renewHandler.HandleAsync(new(
            connection.Id,
            CredentialId,
            new(proof with { WorkerId = Guid.NewGuid() }, 60)), CancellationToken.None);
        clock.UtcNow = Now.AddSeconds(20);
        var renewed = await renewHandler.HandleAsync(new(
            connection.Id, CredentialId, new(proof, 60)), CancellationToken.None);
        CompleteRemoteAdapterRunCommandHandler completeHandler = new(
            new FakeConnectionRepository(connection), runs, clock);
        AdapterRemoteRunCompletionRequest completion = new(
            proof,
            AdapterRunOutcome.Succeeded,
            0,
            0,
            0,
            AcceptedCheckpoint: null,
            ErrorCode: null);
        var completed = await completeHandler.HandleAsync(
            new(connection.Id, CredentialId, completion), CancellationToken.None);
        Assert.True(connection.AdvanceCheckpoint(
            "newer-run-checkpoint", connection.Version, clock.UtcNow).IsSuccess);
        var repeated = await completeHandler.HandleAsync(
            new(connection.Id, CredentialId, completion), CancellationToken.None);

        Assert.Equal(BunkFy.Modules.Ingestion.Domain.Errors.IngestionDomainErrors.RemoteLeaseMismatch, wrong.Error);
        Assert.True(renewed.IsSuccess, renewed.Error.Code);
        Assert.True(completed.IsSuccess, completed.Error.Code);
        Assert.True(repeated.IsSuccess, repeated.Error.Code);
        Assert.Equal(IngestionRunState.Succeeded, runs.Items[0].State);
        Assert.Null(connection.RemoteLeaseId);
    }

    [Fact]
    public async Task Claim_rejects_descriptor_drift_before_allocating_ids()
    {
        AdapterConnection connection = CreateConnection();
        QueueIds ids = new(Guid.NewGuid(), Guid.NewGuid());
        var result = await new ClaimRemoteAdapterLeaseCommandHandler(
            new FakeConnectionRepository(connection),
            new ActivePropertyProjection(),
            new FakeRunRepository(),
            new DescriptorRegistry(),
            new TestScope(),
            new MutableClock(Now),
            ids).HandleAsync(new(
                connection.Id,
                CredentialId,
                new(Guid.NewGuid(), WorkerId, "fake.http", 2, 1, 60)), CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.RemoteLeaseDescriptorMismatch, result.Error);
        Assert.Equal(0, ids.Calls);
    }

    private static AdapterConnection CreateConnection() => AdapterConnection.Create(
        Guid.NewGuid(), "tenant-a", Guid.NewGuid(), "fake.http", AdapterExecutionMode.RemotePolling,
        IngestionConflictPolicy.SuggestionsOnly, "configuration://main", "secret://main", Now).Value;

    private sealed class FakeConnectionRepository(AdapterConnection connection) : IAdapterConnectionRepository
    {
        public Task<AdapterConnection?> GetAsync(Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<AdapterConnection?>(connection.Id == connectionId ? connection : null);

        public Task<AdapterConnection?> GetAsync(
            Guid propertyId, Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<AdapterConnection?>(
                connection.Id == connectionId && connection.PropertyId == propertyId ? connection : null);

        public Task AddAsync(AdapterConnection added, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeRunRepository : IIngestionRunRepository
    {
        public List<IngestionRun> Items { get; } = [];

        public Task<IngestionRun?> GetAsync(Guid runId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(run => run.Id == runId));

        public Task<IngestionRun?> FindByTaskExecutionAsync(
            Guid taskRunId, int taskAttempt, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(run =>
                run.TaskRunId == taskRunId && run.TaskAttempt == taskAttempt));

        public Task<IngestionRun?> FindActiveByConnectionAsync(
            Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult(this.Items.SingleOrDefault(run =>
                run.ConnectionId == connectionId && run.State == IngestionRunState.Running));

        public Task AddAsync(IngestionRun run, CancellationToken cancellationToken)
        {
            this.Items.Add(run);
            return Task.CompletedTask;
        }
    }

    private sealed class ActivePropertyProjection : IIngestionPropertyProjectionRepository
    {
        public Task<bool> IsActiveAsync(Guid propertyId, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task ApplyAsync(
            IngestionPropertyProjectionWriteModel property, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class DescriptorRegistry : IAdapterDescriptorRegistry
    {
        private static readonly AdapterDescriptor Descriptor = new(
            "fake.http", 1, 1,
            [AdapterExecutionMode.Polling, AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling]);

        public IReadOnlyCollection<AdapterDescriptor> GetAll() => [Descriptor];

        public bool TryGet(string adapterType, out AdapterDescriptor? descriptor)
        {
            descriptor = string.Equals(adapterType, Descriptor.AdapterType, StringComparison.Ordinal)
                ? Descriptor
                : null;
            return descriptor is not null;
        }
    }

    private sealed class TestScope : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class QueueIds(params Guid[] values) : IIdGenerator
    {
        private readonly Queue<Guid> values = new(values);
        public int Calls { get; private set; }

        public Guid NewId()
        {
            this.Calls++;
            return this.values.Dequeue();
        }
    }
}
