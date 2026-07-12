namespace BunkFy.Adapter.Runtime;

using BunkFy.Adapter.Abstractions;

public sealed class StandaloneAdapterCycleRunner(
    IAdapterRunner runner,
    IAdapterPushObservationSink pushSink,
    IAdapterCheckpointLease checkpointLease,
    IAdapterRuntimeMaterialProvider materialProvider,
    AdapterRuntimeIdentity identity,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<AdapterRunCompletion> RunAsync(CancellationToken cancellationToken)
    {
        this.ValidateDescriptor();
        DateTimeOffset assignedAtUtc = this.clock.GetUtcNow();
        AdapterRunAssignment assignment = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            identity.ConnectionId,
            identity.ScopeId,
            identity.PropertyId,
            identity.AdapterType,
            AdapterExecutionMode.Polling,
            assignedAtUtc,
            assignedAtUtc.Add(identity.AssignmentLease),
            checkpointLease.Checkpoint);
        CheckpointingPushObservationSink sink = new(
            assignment,
            pushSink,
            checkpointLease,
            this.clock);

        using AdapterConfigurationMaterial material = await materialProvider.ResolveAsync(
            identity,
            runner.Descriptor.ConfigurationSchemaVersion,
            cancellationToken).ConfigureAwait(false);
        if (material.SchemaVersion != runner.Descriptor.ConfigurationSchemaVersion)
        {
            throw new AdapterRuntimeProtocolException(
                "Resolved configuration material does not match the runner schema version.");
        }

        AdapterRunCompletion completion = await runner.RunAsync(
            assignment,
            material,
            sink,
            cancellationToken).ConfigureAwait(false);
        if (completion.RunId != assignment.RunId || completion.LeaseId != assignment.LeaseId)
        {
            throw new AdapterRuntimeProtocolException(
                "The adapter completion does not match the local run assignment.");
        }

        if (!string.Equals(
                completion.AcceptedCheckpoint,
                sink.LastAcceptedCheckpoint,
                StringComparison.Ordinal))
        {
            throw new AdapterRuntimeProtocolException(
                "The adapter completion checkpoint does not match its durable acknowledgement.");
        }

        return completion;
    }

    private void ValidateDescriptor()
    {
        AdapterDescriptor descriptor = runner.Descriptor;
        if (!string.Equals(descriptor.AdapterType, identity.AdapterType, StringComparison.Ordinal) ||
            !descriptor.ExecutionModes.Contains(AdapterExecutionMode.Polling) ||
            !descriptor.ExecutionModes.Contains(AdapterExecutionMode.Push))
        {
            throw new AdapterRuntimeProtocolException(
                "A standalone polling runner must match the configured adapter type and support polling plus push delivery.");
        }
    }

    private sealed class CheckpointingPushObservationSink(
        AdapterRunAssignment assignment,
        IAdapterPushObservationSink pushSink,
        IAdapterCheckpointLease checkpointLease,
        TimeProvider clock)
        : IAdapterObservationSink
    {
        public string? LastAcceptedCheckpoint { get; private set; } = assignment.Checkpoint;

        public async Task<AdapterObservationAcknowledgement> SubmitAsync(
            AdapterObservationSubmission submission,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(submission);
            if (submission.RunId != assignment.RunId || submission.LeaseId != assignment.LeaseId)
            {
                return RejectAssignment(submission);
            }

            AdapterIngressSubmissionResponse response = await pushSink.SubmitAsync(
                submission.Records,
                cancellationToken).ConfigureAwait(false);
            AdapterObservationResult[] results = response.Results?.ToArray() ?? [];
            Guid[] submittedIds = submission.Records.Select(record => record.OperationId).Order().ToArray();
            Guid[] resultIds = results.Select(result => result.OperationId).Distinct().Order().ToArray();
            if (results.Length != submission.Records.Count ||
                resultIds.Length != results.Length ||
                !submittedIds.SequenceEqual(resultIds))
            {
                throw new AdapterRuntimeProtocolException(
                    "The push acknowledgement does not match the adapter submission.");
            }

            bool checkpointAccepted = submission.ProposedCheckpoint is not null &&
                results.All(result => result.Disposition is
                    AdapterObservationDisposition.Accepted or AdapterObservationDisposition.Duplicate);
            if (checkpointAccepted)
            {
                await checkpointLease.SaveAsync(
                    submission.ProposedCheckpoint!,
                    clock.GetUtcNow(),
                    cancellationToken).ConfigureAwait(false);
                this.LastAcceptedCheckpoint = submission.ProposedCheckpoint;
            }

            return new AdapterObservationAcknowledgement(
                submission.RunId,
                submission.LeaseId,
                results,
                checkpointAccepted,
                checkpointAccepted ? submission.ProposedCheckpoint : null);
        }

        private static AdapterObservationAcknowledgement RejectAssignment(
            AdapterObservationSubmission submission) => new(
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
}
