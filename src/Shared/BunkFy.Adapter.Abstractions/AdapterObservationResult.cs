namespace BunkFy.Adapter.Abstractions;

public sealed record AdapterObservationResult
{
    public AdapterObservationResult(
        Guid operationId,
        AdapterObservationDisposition disposition,
        Guid? receiptId,
        string? errorCode)
    {
        this.OperationId = AdapterProtocolGuards.Required(operationId, nameof(operationId));
        if (disposition == AdapterObservationDisposition.Unknown || !Enum.IsDefined(disposition))
        {
            throw new ArgumentOutOfRangeException(nameof(disposition));
        }

        bool requiresReceipt = disposition is
            AdapterObservationDisposition.Accepted or AdapterObservationDisposition.Duplicate;
        if (requiresReceipt && (!receiptId.HasValue || receiptId.Value == Guid.Empty))
        {
            throw new ArgumentException("Accepted and duplicate observations require a receipt id.", nameof(receiptId));
        }

        this.Disposition = disposition;
        this.ReceiptId = receiptId;
        this.ErrorCode = string.IsNullOrWhiteSpace(errorCode)
            ? null
            : AdapterProtocolGuards.StableKey(
                errorCode,
                AdapterProtocolLimits.ErrorCodeMaxLength,
                nameof(errorCode));
    }

    public Guid OperationId { get; }
    public AdapterObservationDisposition Disposition { get; }
    public Guid? ReceiptId { get; }
    public string? ErrorCode { get; }
}
