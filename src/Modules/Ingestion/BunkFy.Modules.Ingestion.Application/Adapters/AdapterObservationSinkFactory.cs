namespace BunkFy.Modules.Ingestion.Application.Adapters;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Application.Commands;

internal sealed class AdapterObservationSinkFactory(IRequestDispatcher dispatcher) : IAdapterObservationSinkFactory
{
    public IAdapterObservationSink Create(AdapterRunAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        return new AssignmentBoundSink(assignment, dispatcher);
    }

    private sealed class AssignmentBoundSink(
        AdapterRunAssignment assignment,
        IRequestDispatcher dispatcher) : IAdapterObservationSink
    {
        public async Task<AdapterObservationAcknowledgement> SubmitAsync(
            AdapterObservationSubmission submission,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(submission);
            if (submission.RunId != assignment.RunId || submission.LeaseId != assignment.LeaseId)
            {
                return RejectAssignment(submission);
            }

            List<AdapterObservationResult> results = [];
            foreach (AdapterObservedRecord record in submission.Records)
            {
                Result<AdapterObservationResult> result = await dispatcher.SendAsync(
                    new ReceiveObservationCommand(
                        assignment.ConnectionId,
                        assignment.RunId,
                        record.OperationId,
                        record.RecordType,
                        record.ExternalRecordId,
                        record.SourceRevision,
                        record.SourceUpdatedAtUtc,
                        record.ObservedAtUtc,
                        record.ContentType,
                        record.Payload,
                        record.ContentSha256),
                    cancellationToken).ConfigureAwait(false);
                results.Add(result.IsSuccess
                    ? result.Value
                    : new AdapterObservationResult(
                        record.OperationId,
                        AdapterObservationDisposition.Rejected,
                        receiptId: null,
                        result.Error.Code));
            }

            bool checkpointAccepted = false;
            string? acceptedCheckpoint = null;
            if (submission.ProposedCheckpoint is not null &&
                results.All(result => result.Disposition != AdapterObservationDisposition.Rejected))
            {
                Result<Unit> checkpoint = await dispatcher.SendAsync(
                    new AdvanceConnectionCheckpointCommand(
                        assignment.ConnectionId,
                        assignment.RunId,
                        submission.ProposedCheckpoint),
                    cancellationToken).ConfigureAwait(false);
                checkpointAccepted = checkpoint.IsSuccess;
                acceptedCheckpoint = checkpoint.IsSuccess ? submission.ProposedCheckpoint : null;
            }

            return new AdapterObservationAcknowledgement(
                submission.RunId,
                submission.LeaseId,
                results,
                checkpointAccepted,
                acceptedCheckpoint);
        }

        private static AdapterObservationAcknowledgement RejectAssignment(AdapterObservationSubmission submission) =>
            new(
                submission.RunId,
                submission.LeaseId,
                submission.Records.Select(record => new AdapterObservationResult(
                    record.OperationId,
                    AdapterObservationDisposition.Rejected,
                    receiptId: null,
                    "ingestion.assignment-mismatch")).ToArray(),
                checkpointAccepted: false,
                acceptedCheckpoint: null);
    }
}
