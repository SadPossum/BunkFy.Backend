namespace BunkFy.Adapter.Abstractions;

public sealed record AdapterObservationAcknowledgement
{
    public AdapterObservationAcknowledgement(
        Guid runId,
        Guid leaseId,
        IReadOnlyCollection<AdapterObservationResult> results,
        bool checkpointAccepted,
        string? acceptedCheckpoint)
    {
        this.RunId = AdapterProtocolGuards.Required(runId, nameof(runId));
        this.LeaseId = AdapterProtocolGuards.Required(leaseId, nameof(leaseId));
        ArgumentNullException.ThrowIfNull(results);
        AdapterObservationResult[] copied = results.ToArray();
        if (copied.Length == 0 || copied.Any(result => result is null) ||
            copied.Select(result => result.OperationId).Distinct().Count() != copied.Length)
        {
            throw new ArgumentException("Acknowledgement results must be non-empty and unique by operation id.", nameof(results));
        }

        string? checkpoint = AdapterProtocolGuards.Optional(
            acceptedCheckpoint,
            AdapterProtocolLimits.CheckpointMaxLength,
            nameof(acceptedCheckpoint));
        if (checkpointAccepted != (checkpoint is not null))
        {
            throw new ArgumentException(
                "An accepted checkpoint value is required exactly when checkpointAccepted is true.",
                nameof(acceptedCheckpoint));
        }

        if (checkpointAccepted && copied.Any(result => result.Disposition == AdapterObservationDisposition.Rejected))
        {
            throw new ArgumentException(
                "A checkpoint cannot advance while a protocol-level observation rejection is present.",
                nameof(checkpointAccepted));
        }

        this.Results = Array.AsReadOnly(copied);
        this.CheckpointAccepted = checkpointAccepted;
        this.AcceptedCheckpoint = checkpoint;
    }

    public Guid RunId { get; }
    public Guid LeaseId { get; }
    public IReadOnlyCollection<AdapterObservationResult> Results { get; }
    public bool CheckpointAccepted { get; }
    public string? AcceptedCheckpoint { get; }
}
