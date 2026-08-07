namespace BunkFy.Modules.Properties.Application.Ports;

using BunkFy.Modules.Properties.Contracts;

public interface IPropertyMutationOperationRepository
{
    Task<PropertyMutationOperationRecord?> GetAsync(
        Guid propertyId,
        Guid operationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        PropertyMutationOperationRecord operation,
        CancellationToken cancellationToken);
}

public sealed record PropertyMutationOperationRecord(
    Guid OperationId,
    string ScopeId,
    Guid PropertyId,
    PropertyMutationKind Kind,
    long ExpectedVersion,
    string RequestFingerprint,
    PropertyStatus ResultStatus,
    PropertyProcessingStatus ResultProcessingStatus,
    long ResultVersion,
    DateTimeOffset CompletedAtUtc)
{
    public bool Matches(
        PropertyMutationKind kind,
        Guid propertyId,
        long expectedVersion,
        string requestFingerprint) =>
        this.Kind == kind &&
        this.PropertyId == propertyId &&
        this.ExpectedVersion == expectedVersion &&
        string.Equals(
            this.RequestFingerprint,
            requestFingerprint,
            StringComparison.Ordinal);

    public PropertyMutationReceiptDto ToReceipt() => new(
        this.PropertyId,
        this.ResultStatus,
        this.ResultProcessingStatus,
        this.ResultVersion);
}

public enum PropertyMutationKind
{
    DetailsUpdate = 1
}
