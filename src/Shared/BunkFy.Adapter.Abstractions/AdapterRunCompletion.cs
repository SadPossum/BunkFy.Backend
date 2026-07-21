namespace BunkFy.Adapter.Abstractions;

public sealed record AdapterRunCompletion
{
    public AdapterRunCompletion(
        Guid runId,
        Guid leaseId,
        AdapterRunOutcome outcome,
        int observedCount,
        int acceptedCount,
        int rejectedCount,
        string? acceptedCheckpoint,
        string? errorCode,
        string? errorMessage)
    {
        this.RunId = AdapterProtocolGuards.Required(runId, nameof(runId));
        this.LeaseId = AdapterProtocolGuards.Required(leaseId, nameof(leaseId));
        if (outcome == AdapterRunOutcome.Unknown || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (observedCount < 0 || acceptedCount < 0 || rejectedCount < 0 ||
            (long)acceptedCount + rejectedCount > observedCount)
        {
            throw new ArgumentException("Adapter run counts cannot be negative or exceed the observed count.");
        }

        this.Outcome = outcome;
        this.ObservedCount = observedCount;
        this.AcceptedCount = acceptedCount;
        this.RejectedCount = rejectedCount;
        this.AcceptedCheckpoint = AdapterProtocolGuards.Optional(
            acceptedCheckpoint,
            AdapterProtocolLimits.CheckpointMaxLength,
            nameof(acceptedCheckpoint));
        string? normalizedErrorCode = string.IsNullOrWhiteSpace(errorCode)
            ? null
            : AdapterProtocolGuards.StableKey(
                errorCode,
                AdapterProtocolLimits.ErrorCodeMaxLength,
                nameof(errorCode));
        string? normalizedErrorMessage = AdapterProtocolGuards.Optional(
            errorMessage,
            AdapterProtocolLimits.ErrorMessageMaxLength,
            nameof(errorMessage));
        bool requiresErrorCode = outcome is AdapterRunOutcome.PartiallySucceeded or
            AdapterRunOutcome.Failed or AdapterRunOutcome.Cancelled;
        if (requiresErrorCode != (normalizedErrorCode is not null) ||
            (outcome == AdapterRunOutcome.Succeeded && normalizedErrorMessage is not null))
        {
            throw new ArgumentException(
                "Non-successful adapter runs require an error code, and successful runs cannot include error details.");
        }

        this.ErrorCode = normalizedErrorCode;
        this.ErrorMessage = normalizedErrorMessage;
    }

    public Guid RunId { get; }
    public Guid LeaseId { get; }
    public AdapterRunOutcome Outcome { get; }
    public int ObservedCount { get; }
    public int AcceptedCount { get; }
    public int RejectedCount { get; }
    public string? AcceptedCheckpoint { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }
}
