namespace BunkFy.Adapters.Tests;

using System.Text;
using BunkFy.Adapter.Abstractions;
using BunkFy.Adapter.Runtime;
using BunkFy.Adapters.FakeHttp;
using BunkFy.Adapters.JsonFileDrop;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StandaloneAdapterRuntimeTests
{
    [Fact]
    public void First_party_polling_adapters_declare_standalone_push_delivery()
    {
        Assert.Contains(AdapterExecutionMode.Polling, FakeHttpAdapterDescriptor.Value.ExecutionModes);
        Assert.Contains(AdapterExecutionMode.Push, FakeHttpAdapterDescriptor.Value.ExecutionModes);
        Assert.Contains(AdapterExecutionMode.RemotePolling, FakeHttpAdapterDescriptor.Value.ExecutionModes);
        Assert.Contains(AdapterExecutionMode.Polling, JsonFileDropAdapterDescriptor.Value.ExecutionModes);
        Assert.Contains(AdapterExecutionMode.Push, JsonFileDropAdapterDescriptor.Value.ExecutionModes);
        Assert.Contains(AdapterExecutionMode.RemotePolling, JsonFileDropAdapterDescriptor.Value.ExecutionModes);
    }

    [Fact]
    public async Task Remote_cycle_uses_server_assignment_checkpoint_and_completion()
    {
        Guid connectionId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid workerId = Guid.NewGuid();
        AdapterRunAssignment assignment = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            connectionId,
            "tenant-a",
            propertyId,
            "test.standalone",
            AdapterExecutionMode.Polling,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(2),
            "cursor-1");
        RecordingRemoteControl remote = new(assignment, leaseEpoch: 7);
        TestRunner runner = new(async (received, sink, token) =>
        {
            Assert.Equal(assignment.RunId, received.RunId);
            Assert.Equal("cursor-1", received.Checkpoint);
            AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
                new AdapterObservationSubmission(
                    received.RunId,
                    received.LeaseId,
                    [CreateRecord()],
                    "cursor-2"),
                token);
            return Completion(received, acknowledgement, AdapterRunOutcome.Succeeded);
        });
        RemoteLeasedAdapterCycleRunner cycle = new(
            runner,
            remote,
            new CapturingMaterialProvider(),
            new AdapterRuntimeIdentity(
                "tenant-a", propertyId, connectionId, "test.standalone", TimeSpan.FromMinutes(5)),
            workerId,
            TimeSpan.FromMinutes(1));

        AdapterRunCompletion completion = await cycle.RunAsync(CancellationToken.None);

        Assert.Equal("cursor-2", completion.AcceptedCheckpoint);
        Assert.Equal("cursor-2", remote.Checkpoint);
        Assert.Equal(1, remote.Submissions);
        Assert.Equal(AdapterRunOutcome.Succeeded, Assert.Single(remote.Completions).Outcome);
        Assert.Equal(workerId, remote.LastClaim!.WorkerId);
        Assert.NotEqual(Guid.Empty, remote.LastClaim.ClaimId);
    }

    [Fact]
    public async Task Remote_cycle_rejects_assignment_for_another_connection_before_material_access()
    {
        AdapterRunAssignment assignment = new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "tenant-a", Guid.NewGuid(),
            "test.standalone", AdapterExecutionMode.Polling, DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(2), checkpoint: null);
        CapturingMaterialProvider material = new();
        RemoteLeasedAdapterCycleRunner cycle = new(
            new TestRunner((_, _, _) => throw new InvalidOperationException("must not run")),
            new RecordingRemoteControl(assignment, 1),
            material,
            new AdapterRuntimeIdentity(
                "tenant-a", assignment.PropertyId, Guid.NewGuid(), "test.standalone", TimeSpan.FromMinutes(5)),
            Guid.NewGuid(),
            TimeSpan.FromMinutes(1));

        await Assert.ThrowsAsync<AdapterRuntimeProtocolException>(() =>
            cycle.RunAsync(CancellationToken.None));

        Assert.Null(material.Material);
    }

    [Theory]
    [InlineData(AdapterObservationDisposition.Accepted)]
    [InlineData(AdapterObservationDisposition.Duplicate)]
    public async Task Durable_acknowledgement_persists_checkpoint_before_runner_receives_it(
        AdapterObservationDisposition disposition)
    {
        MemoryCheckpointLease checkpoint = new();
        TestRunner runner = new(async (assignment, sink, token) =>
        {
            AdapterObservedRecord record = CreateRecord();
            AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
                new AdapterObservationSubmission(
                    assignment.RunId,
                    assignment.LeaseId,
                    [record],
                    "cursor-2"),
                token);
            Assert.Equal("cursor-2", checkpoint.Checkpoint);
            Assert.True(acknowledgement.CheckpointAccepted);
            return Completion(assignment, acknowledgement, AdapterRunOutcome.Succeeded);
        });
        CapturingMaterialProvider material = new();
        StandaloneAdapterCycleRunner cycle = CreateCycle(
            runner,
            new ResultPushSink(disposition),
            checkpoint,
            material);

        AdapterRunCompletion completion = await cycle.RunAsync(CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Succeeded, completion.Outcome);
        Assert.Equal("cursor-2", completion.AcceptedCheckpoint);
        Assert.Equal(1, checkpoint.Generation);
        Assert.Throws<ObjectDisposedException>(() => _ = material.Material!.Configuration);
    }

    [Fact]
    public async Task Rejected_observation_does_not_advance_checkpoint()
    {
        MemoryCheckpointLease checkpoint = new();
        TestRunner runner = new(async (assignment, sink, token) =>
        {
            AdapterObservedRecord record = CreateRecord();
            AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
                new AdapterObservationSubmission(
                    assignment.RunId,
                    assignment.LeaseId,
                    [record],
                    "cursor-rejected"),
                token);
            Assert.False(acknowledgement.CheckpointAccepted);
            return Completion(assignment, acknowledgement, AdapterRunOutcome.PartiallySucceeded);
        });
        StandaloneAdapterCycleRunner cycle = CreateCycle(
            runner,
            new ResultPushSink(AdapterObservationDisposition.Rejected),
            checkpoint,
            new CapturingMaterialProvider());

        AdapterRunCompletion completion = await cycle.RunAsync(CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Null(checkpoint.Checkpoint);
        Assert.Equal(0, checkpoint.Generation);
    }

    [Fact]
    public async Task Checkpoint_failure_after_remote_acceptance_fails_cycle_for_safe_replay()
    {
        MemoryCheckpointLease checkpoint = new() { FailWrites = true };
        TestRunner runner = new(async (assignment, sink, token) =>
        {
            AdapterObservedRecord record = CreateRecord();
            _ = await sink.SubmitAsync(
                new AdapterObservationSubmission(
                    assignment.RunId,
                    assignment.LeaseId,
                    [record],
                    "cursor-uncertain"),
                token);
            throw new InvalidOperationException("The sink should fail before returning acknowledgement.");
        });
        ResultPushSink push = new(AdapterObservationDisposition.Accepted);
        StandaloneAdapterCycleRunner cycle = CreateCycle(
            runner,
            push,
            checkpoint,
            new CapturingMaterialProvider());

        await Assert.ThrowsAsync<AdapterCheckpointException>(() =>
            cycle.RunAsync(CancellationToken.None));

        Assert.Equal(1, push.Submissions);
        Assert.Null(checkpoint.Checkpoint);
    }

    [Fact]
    public async Task Completion_identity_and_checkpoint_must_match_durable_acknowledgement()
    {
        TestRunner runner = new(async (assignment, sink, token) =>
        {
            AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
                new AdapterObservationSubmission(
                    assignment.RunId,
                    assignment.LeaseId,
                    [CreateRecord()],
                    "cursor-2"),
                token);
            return new AdapterRunCompletion(
                assignment.RunId,
                assignment.LeaseId,
                AdapterRunOutcome.Succeeded,
                1,
                1,
                0,
                "different-cursor",
                errorCode: null,
                errorMessage: null);
        });
        StandaloneAdapterCycleRunner cycle = CreateCycle(
            runner,
            new ResultPushSink(AdapterObservationDisposition.Accepted),
            new MemoryCheckpointLease(),
            new CapturingMaterialProvider());

        await Assert.ThrowsAsync<AdapterRuntimeProtocolException>(() =>
            cycle.RunAsync(CancellationToken.None));
    }

    [Fact]
    public async Task File_checkpoint_store_is_atomic_connection_bound_and_exclusively_leased()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"bunkfy-adapter-runtime-{Guid.NewGuid():N}");
        string statePath = Path.Combine(directory, "checkpoint.json");
        Guid connectionId = Guid.NewGuid();
        try
        {
            FileAdapterCheckpointStore store = new(statePath);
            await using (IAdapterCheckpointLease first = await store.AcquireAsync(
                             connectionId, CancellationToken.None))
            {
                await Assert.ThrowsAsync<AdapterCheckpointException>(async () =>
                    await store.AcquireAsync(connectionId, CancellationToken.None));
                await first.SaveAsync(
                    "cursor-42",
                    new DateTimeOffset(2026, 7, 12, 12, 0, 0, TimeSpan.Zero),
                    CancellationToken.None);
                Assert.Equal("cursor-42", first.Checkpoint);
                Assert.Equal(1, first.Generation);
                Assert.Empty(Directory.GetFiles(directory, "*.tmp.*"));
            }

            await using (IAdapterCheckpointLease reopened = await store.AcquireAsync(
                             connectionId, CancellationToken.None))
            {
                Assert.Equal("cursor-42", reopened.Checkpoint);
                Assert.Equal(1, reopened.Generation);
            }

            await Assert.ThrowsAsync<AdapterCheckpointException>(async () =>
                await store.AcquireAsync(Guid.NewGuid(), CancellationToken.None));
            await File.WriteAllTextAsync(statePath, "{\"schemaVersion\":99}");
            await Assert.ThrowsAsync<AdapterCheckpointException>(async () =>
                await store.AcquireAsync(connectionId, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task File_material_provider_reloads_bounded_configuration_and_secret_each_cycle()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"bunkfy-adapter-material-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string configurationPath = Path.Combine(directory, "adapter.json");
        string secretPath = Path.Combine(directory, "secret.json");
        try
        {
            await File.WriteAllTextAsync(configurationPath, "{\"revision\":1}");
            await File.WriteAllTextAsync(secretPath, "{\"token\":\"first\"}");
            FileAdapterRuntimeMaterialProvider provider = new(new FileAdapterRuntimeMaterialOptions(
                configurationPath,
                "application/json",
                secretPath,
                "application/json"));
            AdapterRuntimeIdentity identity = new(
                "tenant-a", Guid.NewGuid(), Guid.NewGuid(), "test.standalone", TimeSpan.FromMinutes(5));

            using (AdapterConfigurationMaterial first = await provider.ResolveAsync(
                       identity, 1, CancellationToken.None))
            {
                Assert.Equal("{\"revision\":1}", Encoding.UTF8.GetString(first.Configuration.Span));
                Assert.Equal("{\"token\":\"first\"}", Encoding.UTF8.GetString(first.Secret.Span));
            }

            await File.WriteAllTextAsync(configurationPath, "{\"revision\":2}");
            await File.WriteAllTextAsync(secretPath, "{\"token\":\"second\"}");
            using AdapterConfigurationMaterial second = await provider.ResolveAsync(
                identity, 1, CancellationToken.None);
            Assert.Equal("{\"revision\":2}", Encoding.UTF8.GetString(second.Configuration.Span));
            Assert.Equal("{\"token\":\"second\"}", Encoding.UTF8.GetString(second.Secret.Span));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static StandaloneAdapterCycleRunner CreateCycle(
        IAdapterRunner runner,
        IAdapterPushObservationSink push,
        IAdapterCheckpointLease checkpoint,
        IAdapterRuntimeMaterialProvider material) => new(
        runner,
        push,
        checkpoint,
        material,
        new AdapterRuntimeIdentity(
            "tenant-a",
            Guid.NewGuid(),
            checkpoint.ConnectionId,
            "test.standalone",
            TimeSpan.FromMinutes(5)));

    private static AdapterObservedRecord CreateRecord()
    {
        byte[] payload = Encoding.UTF8.GetBytes("{\"reservation\":42}");
        return new AdapterObservedRecord(
            Guid.NewGuid(),
            "reservation.v1",
            "booking-42",
            "2",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            "application/json",
            payload,
            AdapterPayloadHash.ComputeSha256(payload));
    }

    private static AdapterRunCompletion Completion(
        AdapterRunAssignment assignment,
        AdapterObservationAcknowledgement acknowledgement,
        AdapterRunOutcome outcome) => new(
        assignment.RunId,
        assignment.LeaseId,
        outcome,
        observedCount: 1,
        acceptedCount: acknowledgement.Results.Count(result => result.Disposition is
            AdapterObservationDisposition.Accepted or AdapterObservationDisposition.Duplicate),
        rejectedCount: acknowledgement.Results.Count(result =>
            result.Disposition == AdapterObservationDisposition.Rejected),
        acknowledgement.AcceptedCheckpoint,
        errorCode: outcome == AdapterRunOutcome.Succeeded ? null : "adapter.observation-rejected",
        errorMessage: null);

    private sealed class TestRunner(
        Func<AdapterRunAssignment, IAdapterObservationSink, CancellationToken, Task<AdapterRunCompletion>> run)
        : IAdapterRunner
    {
        public AdapterDescriptor Descriptor { get; } = new(
            "test.standalone",
            protocolVersion: 1,
            configurationSchemaVersion: 1,
            [AdapterExecutionMode.Polling, AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling],
            new AdapterPollingCapability(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));

        public Task<AdapterRunCompletion> RunAsync(
            AdapterRunAssignment assignment,
            AdapterConfigurationMaterial material,
            IAdapterObservationSink sink,
            CancellationToken cancellationToken) => run(assignment, sink, cancellationToken);
    }

    private sealed class ResultPushSink(AdapterObservationDisposition disposition)
        : IAdapterPushObservationSink
    {
        public int Submissions { get; private set; }

        public Task<AdapterIngressSubmissionResponse> SubmitAsync(
            IReadOnlyCollection<AdapterObservedRecord> records,
            CancellationToken cancellationToken)
        {
            this.Submissions++;
            return Task.FromResult(new AdapterIngressSubmissionResponse(records.Select(record =>
                new AdapterObservationResult(
                    record.OperationId,
                    disposition,
                    disposition is AdapterObservationDisposition.Accepted or AdapterObservationDisposition.Duplicate
                        ? Guid.NewGuid()
                        : null,
                    disposition == AdapterObservationDisposition.Rejected ? "source.rejected" : null)).ToArray()));
        }
    }

    private sealed class MemoryCheckpointLease : IAdapterCheckpointLease
    {
        public Guid ConnectionId { get; } = Guid.NewGuid();
        public string? Checkpoint { get; private set; }
        public long Generation { get; private set; }
        public bool FailWrites { get; init; }

        public Task SaveAsync(
            string checkpoint,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken)
        {
            if (this.FailWrites)
            {
                throw new AdapterCheckpointException("Simulated checkpoint failure.");
            }

            this.Checkpoint = checkpoint;
            this.Generation++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CapturingMaterialProvider : IAdapterRuntimeMaterialProvider
    {
        public AdapterConfigurationMaterial? Material { get; private set; }

        public Task<AdapterConfigurationMaterial> ResolveAsync(
            AdapterRuntimeIdentity identity,
            int configurationSchemaVersion,
            CancellationToken cancellationToken)
        {
            this.Material = new AdapterConfigurationMaterial(
                configurationSchemaVersion,
                "application/json",
                "{}"u8);
            return Task.FromResult(this.Material);
        }
    }

    private sealed class RecordingRemoteControl(
        AdapterRunAssignment assignment,
        long leaseEpoch) : IAdapterRemoteControlClient
    {
        public AdapterRemoteLeaseClaimRequest? LastClaim { get; private set; }
        public int Submissions { get; private set; }
        public string? Checkpoint { get; private set; } = assignment.Checkpoint;
        public List<AdapterRemoteRunCompletionRequest> Completions { get; } = [];

        public Task<AdapterRemoteLeaseClaimResponse> ClaimAsync(
            AdapterRemoteLeaseClaimRequest request,
            CancellationToken cancellationToken)
        {
            this.LastClaim = request;
            return Task.FromResult(new AdapterRemoteLeaseClaimResponse(
                assignment,
                leaseEpoch,
                RenewAfterSeconds: 20));
        }

        public Task<AdapterRemoteLeaseRenewResponse> RenewAsync(
            AdapterRemoteLeaseRenewRequest request,
            CancellationToken cancellationToken) => Task.FromResult(new AdapterRemoteLeaseRenewResponse(
            request.Lease.RunId,
            request.Lease.LeaseId,
            request.Lease.LeaseEpoch,
            DateTimeOffset.UtcNow.AddMinutes(1),
            RenewAfterSeconds: 20));

        public Task<AdapterRemoteObservationSubmissionResponse> SubmitAsync(
            AdapterRemoteObservationSubmissionRequest request,
            CancellationToken cancellationToken)
        {
            this.Submissions++;
            this.Checkpoint = request.ProposedCheckpoint;
            return Task.FromResult(new AdapterRemoteObservationSubmissionResponse(
                new AdapterObservationAcknowledgement(
                    request.Lease.RunId,
                    request.Lease.LeaseId,
                    request.Records.Select(record => new AdapterObservationResult(
                        record.OperationId,
                        AdapterObservationDisposition.Accepted,
                        Guid.NewGuid(),
                        errorCode: null)).ToArray(),
                    checkpointAccepted: true,
                    request.ProposedCheckpoint)));
        }

        public Task<AdapterRemoteRunCompletionResponse> CompleteAsync(
            AdapterRemoteRunCompletionRequest request,
            CancellationToken cancellationToken)
        {
            this.Completions.Add(request);
            return Task.FromResult(new AdapterRemoteRunCompletionResponse(
                request.Lease.RunId,
                request.Lease.LeaseId,
                request.Lease.LeaseEpoch,
                request.Outcome,
                request.AcceptedCheckpoint,
                DateTimeOffset.UtcNow));
        }
    }
}
