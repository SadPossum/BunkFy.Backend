namespace BunkFy.Adapter.Abstractions;

public sealed record AdapterObservationSubmission
{
    public AdapterObservationSubmission(
        Guid runId,
        Guid leaseId,
        IReadOnlyCollection<AdapterObservedRecord> records,
        string? proposedCheckpoint)
    {
        this.RunId = AdapterProtocolGuards.Required(runId, nameof(runId));
        this.LeaseId = AdapterProtocolGuards.Required(leaseId, nameof(leaseId));
        ArgumentNullException.ThrowIfNull(records);
        AdapterObservedRecord[] copied = records.ToArray();
        if (copied.Length is 0 or > AdapterProtocolLimits.MaximumRecordsPerSubmission ||
            copied.Any(record => record is null) ||
            copied.Select(record => record.OperationId).Distinct().Count() != copied.Length ||
            copied.Sum(record => (long)record.Payload.Length) > AdapterProtocolLimits.MaximumSubmissionPayloadBytes)
        {
            throw new ArgumentException(
                $"A submission requires 1 to {AdapterProtocolLimits.MaximumRecordsPerSubmission} records with unique operation ids.",
                nameof(records));
        }

        this.Records = Array.AsReadOnly(copied);
        this.ProposedCheckpoint = AdapterProtocolGuards.Optional(
            proposedCheckpoint,
            AdapterProtocolLimits.CheckpointMaxLength,
            nameof(proposedCheckpoint));
    }

    public Guid RunId { get; }
    public Guid LeaseId { get; }
    public IReadOnlyCollection<AdapterObservedRecord> Records { get; }
    public string? ProposedCheckpoint { get; }
}
