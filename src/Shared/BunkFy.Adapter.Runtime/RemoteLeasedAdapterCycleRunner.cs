namespace BunkFy.Adapter.Runtime;

using BunkFy.Adapter.Abstractions;

public sealed class RemoteLeasedAdapterCycleRunner(
    IAdapterRunner runner,
    IAdapterRemoteControlClient remoteControl,
    IAdapterRuntimeMaterialProvider materialProvider,
    AdapterRuntimeIdentity identity,
    Guid workerId,
    TimeSpan requestedLease,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<AdapterRunCompletion> RunAsync(CancellationToken cancellationToken)
    {
        this.ValidateConfiguration();
        AdapterDescriptor descriptor = runner.Descriptor;
        AdapterRemoteLeaseClaimResponse claimed = await remoteControl.ClaimAsync(
            new AdapterRemoteLeaseClaimRequest(
                Guid.NewGuid(),
                workerId,
                descriptor.AdapterType,
                descriptor.ProtocolVersion,
                descriptor.ConfigurationSchemaVersion,
                checked((int)requestedLease.TotalSeconds)),
            cancellationToken).ConfigureAwait(false);
        this.ValidateAssignment(claimed);

        AdapterRemoteLeaseProof proof = new(
            claimed.Assignment.RunId,
            claimed.Assignment.LeaseId,
            claimed.LeaseEpoch,
            workerId);
        await using RemoteLeaseSession session = new(
            remoteControl,
            proof,
            requestedLease,
            claimed.RenewAfterSeconds,
            claimed.Assignment.Checkpoint,
            this.clock);
        session.Start();

        using CancellationTokenSource runCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, session.LeaseLostToken);
        try
        {
            using AdapterConfigurationMaterial material = await materialProvider.ResolveAsync(
                identity,
                descriptor.ConfigurationSchemaVersion,
                runCancellation.Token).ConfigureAwait(false);
            if (material.SchemaVersion != descriptor.ConfigurationSchemaVersion)
            {
                throw new AdapterRuntimeProtocolException(
                    "Resolved configuration material does not match the runner schema version.");
            }

            RemoteObservationSink sink = new(session, claimed.Assignment);
            AdapterRunCompletion completion = await runner.RunAsync(
                claimed.Assignment,
                material,
                sink,
                runCancellation.Token).ConfigureAwait(false);
            ValidateCompletion(completion, claimed.Assignment, sink.LastAcceptedCheckpoint);
            await session.StopHeartbeatAsync().ConfigureAwait(false);
            await session.CompleteAsync(completion, cancellationToken).ConfigureAwait(false);
            return completion;
        }
        catch (OperationCanceledException exception) when (
            session.LeaseLostToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            throw new AdapterRemoteLeaseLostException(
                "The server-owned adapter lease was lost during execution.", exception);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await session.StopHeartbeatAsync().ConfigureAwait(false);
            if (!session.LeaseLostToken.IsCancellationRequested)
            {
                await session.TryTerminateAsync(
                    AdapterRunOutcome.Cancelled,
                    "adapter.runtime-cancelled").ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception)
        {
            await session.StopHeartbeatAsync().ConfigureAwait(false);
            if (!session.LeaseLostToken.IsCancellationRequested)
            {
                await session.TryTerminateAsync(
                    AdapterRunOutcome.Failed,
                    "adapter.runtime-failed").ConfigureAwait(false);
            }

            throw;
        }
    }

    private void ValidateConfiguration()
    {
        if (workerId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty remote adapter worker id is required.", nameof(workerId));
        }

        if (requestedLease < TimeSpan.FromSeconds(AdapterRemoteLeaseContractLimits.MinimumLeaseSeconds) ||
            requestedLease > TimeSpan.FromSeconds(AdapterRemoteLeaseContractLimits.MaximumLeaseSeconds) ||
            requestedLease.TotalSeconds != Math.Truncate(requestedLease.TotalSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(requestedLease));
        }

        AdapterDescriptor descriptor = runner.Descriptor;
        if (!string.Equals(descriptor.AdapterType, identity.AdapterType, StringComparison.Ordinal) ||
            !descriptor.ExecutionModes.Contains(AdapterExecutionMode.Polling) ||
            !descriptor.ExecutionModes.Contains(AdapterExecutionMode.RemotePolling))
        {
            throw new AdapterRuntimeProtocolException(
                "A remote polling runner must match the configured adapter type and declare remote polling support.");
        }
    }

    private void ValidateAssignment(AdapterRemoteLeaseClaimResponse claimed)
    {
        AdapterRunAssignment assignment = claimed.Assignment ??
            throw new AdapterRuntimeProtocolException("The remote lease claim returned no assignment.");
        DateTimeOffset nowUtc = this.clock.GetUtcNow();
        if (assignment.ConnectionId != identity.ConnectionId || assignment.PropertyId != identity.PropertyId ||
            !string.Equals(assignment.ScopeId, identity.ScopeId, StringComparison.Ordinal) ||
            !string.Equals(assignment.AdapterType, identity.AdapterType, StringComparison.Ordinal) ||
            assignment.ExecutionMode != AdapterExecutionMode.Polling || claimed.LeaseEpoch <= 0 ||
            claimed.RenewAfterSeconds <= 0 || assignment.LeaseExpiresAtUtc <= nowUtc ||
            assignment.LeaseExpiresAtUtc - nowUtc <= TimeSpan.FromSeconds(claimed.RenewAfterSeconds))
        {
            throw new AdapterRuntimeProtocolException(
                "The remote lease assignment does not match the configured runtime identity.");
        }
    }

    private static void ValidateCompletion(
        AdapterRunCompletion completion,
        AdapterRunAssignment assignment,
        string? acceptedCheckpoint)
    {
        if (completion.RunId != assignment.RunId || completion.LeaseId != assignment.LeaseId ||
            !string.Equals(completion.AcceptedCheckpoint, acceptedCheckpoint, StringComparison.Ordinal))
        {
            throw new AdapterRuntimeProtocolException(
                "The adapter completion does not match its remote assignment and server checkpoint acknowledgement.");
        }
    }

    private sealed class RemoteObservationSink(
        RemoteLeaseSession session,
        AdapterRunAssignment assignment) : IAdapterObservationSink
    {
        public string? LastAcceptedCheckpoint { get; private set; } = assignment.Checkpoint;

        public async Task<AdapterObservationAcknowledgement> SubmitAsync(
            AdapterObservationSubmission submission,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(submission);
            if (submission.RunId != assignment.RunId || submission.LeaseId != assignment.LeaseId)
            {
                return Reject(submission);
            }

            AdapterRemoteObservationSubmissionResponse response = await session.SubmitAsync(
                submission,
                cancellationToken).ConfigureAwait(false);
            AdapterObservationAcknowledgement acknowledgement = response.Acknowledgement;
            if (acknowledgement.RunId != assignment.RunId || acknowledgement.LeaseId != assignment.LeaseId)
            {
                throw new AdapterRuntimeProtocolException(
                    "The leased observation acknowledgement does not match the assignment.");
            }

            if (acknowledgement.CheckpointAccepted)
            {
                this.LastAcceptedCheckpoint = acknowledgement.AcceptedCheckpoint;
            }

            return acknowledgement;
        }

        private static AdapterObservationAcknowledgement Reject(AdapterObservationSubmission submission) => new(
            submission.RunId,
            submission.LeaseId,
            submission.Records.Select(record => new AdapterObservationResult(
                record.OperationId,
                AdapterObservationDisposition.Rejected,
                receiptId: null,
                "adapter.assignment-mismatch")).ToArray(),
            checkpointAccepted: false,
            acceptedCheckpoint: null);
    }

    private sealed class RemoteLeaseSession(
        IAdapterRemoteControlClient remoteControl,
        AdapterRemoteLeaseProof proof,
        TimeSpan requestedLease,
        int initialRenewAfterSeconds,
        string? initialCheckpoint,
        TimeProvider clock) : IAsyncDisposable
    {
        private readonly SemaphoreSlim gate = new(1, 1);
        private readonly CancellationTokenSource heartbeatStop = new();
        private readonly CancellationTokenSource leaseLost = new();
        private Task? heartbeat;
        private int renewAfterSeconds = initialRenewAfterSeconds;
        private string? acceptedCheckpoint = initialCheckpoint;

        public CancellationToken LeaseLostToken => this.leaseLost.Token;

        public void Start() => this.heartbeat = this.RunHeartbeatAsync();

        public async Task<AdapterRemoteObservationSubmissionResponse> SubmitAsync(
            AdapterObservationSubmission submission,
            CancellationToken cancellationToken)
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                this.ThrowIfLost();
                AdapterRemoteObservationSubmissionResponse response = await remoteControl.SubmitAsync(
                    new AdapterRemoteObservationSubmissionRequest(
                        proof,
                        submission.Records.Select(AdapterIngressObservationRequest.FromRecord).ToArray(),
                        submission.ProposedCheckpoint),
                    cancellationToken).ConfigureAwait(false);
                if (response.Acknowledgement.CheckpointAccepted)
                {
                    this.acceptedCheckpoint = response.Acknowledgement.AcceptedCheckpoint;
                }

                return response;
            }
            finally
            {
                this.gate.Release();
            }
        }

        public async Task CompleteAsync(
            AdapterRunCompletion completion,
            CancellationToken cancellationToken)
        {
            await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                this.ThrowIfLost();
                _ = await remoteControl.CompleteAsync(
                    this.ToRequest(completion), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                this.gate.Release();
            }
        }

        public async Task TryTerminateAsync(AdapterRunOutcome outcome, string errorCode)
        {
            using CancellationTokenSource cleanup = new(TimeSpan.FromSeconds(10), clock);
            try
            {
                await this.gate.WaitAsync(cleanup.Token).ConfigureAwait(false);
                try
                {
                    _ = await remoteControl.CompleteAsync(
                        new AdapterRemoteRunCompletionRequest(
                            proof,
                            outcome,
                            0,
                            0,
                            0,
                            this.acceptedCheckpoint,
                            errorCode),
                        cleanup.Token).ConfigureAwait(false);
                }
                finally
                {
                    this.gate.Release();
                }
            }
            catch (Exception)
            {
                // The lease expiry path terminalizes an unreported runtime failure.
            }
        }

        public async Task StopHeartbeatAsync()
        {
            if (!this.heartbeatStop.IsCancellationRequested)
            {
                this.heartbeatStop.Cancel();
            }

            if (this.heartbeat is not null)
            {
                await this.heartbeat.ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await this.StopHeartbeatAsync().ConfigureAwait(false);
            this.heartbeatStop.Dispose();
            this.leaseLost.Dispose();
            this.gate.Dispose();
        }

        private async Task RunHeartbeatAsync()
        {
            try
            {
                while (!this.heartbeatStop.IsCancellationRequested)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(this.renewAfterSeconds),
                        clock,
                        this.heartbeatStop.Token).ConfigureAwait(false);
                    await this.gate.WaitAsync(this.heartbeatStop.Token).ConfigureAwait(false);
                    try
                    {
                        AdapterRemoteLeaseRenewResponse renewed = await remoteControl.RenewAsync(
                            new AdapterRemoteLeaseRenewRequest(
                                proof,
                                checked((int)requestedLease.TotalSeconds)),
                            this.heartbeatStop.Token).ConfigureAwait(false);
                        this.renewAfterSeconds = renewed.RenewAfterSeconds;
                    }
                    finally
                    {
                        this.gate.Release();
                    }
                }
            }
            catch (OperationCanceledException) when (this.heartbeatStop.IsCancellationRequested)
            {
            }
            catch (Exception)
            {
                this.leaseLost.Cancel();
            }
        }

        private void ThrowIfLost()
        {
            if (this.leaseLost.IsCancellationRequested)
            {
                throw new AdapterRemoteLeaseLostException("The server-owned adapter lease was lost.");
            }
        }

        private AdapterRemoteRunCompletionRequest ToRequest(AdapterRunCompletion completion) => new(
            proof,
            completion.Outcome,
            completion.ObservedCount,
            completion.AcceptedCount,
            completion.RejectedCount,
            completion.AcceptedCheckpoint,
            completion.ErrorCode);
    }
}
