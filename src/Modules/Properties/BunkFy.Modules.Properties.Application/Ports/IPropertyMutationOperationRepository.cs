namespace BunkFy.Modules.Properties.Application.Ports;

using BunkFy.Modules.Properties.Contracts;

public interface IPropertyMutationOperationRepository
{
    Task<PropertyMutationOperationRecord?> GetAsync(
        PropertyMutationResourceKind resourceKind,
        Guid resourceId,
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
    PropertyMutationResourceKind ResourceKind,
    Guid ResourceId,
    PropertyMutationKind Kind,
    long ExpectedVersion,
    string RequestFingerprint,
    PropertyStatus? ResultStatus,
    PropertyProcessingStatus? ResultProcessingStatus,
    Guid? ResultRoomId,
    RoomStatus? ResultRoomStatus,
    Guid? ResultBedId,
    BedStatus? ResultBedStatus,
    int? ResultAffectedBedCount,
    long ResultVersion,
    long ResultResourceVersion,
    DateTimeOffset CompletedAtUtc)
{
    public bool Matches(
        PropertyMutationKind kind,
        Guid propertyId,
        PropertyMutationResourceKind resourceKind,
        Guid resourceId,
        long expectedVersion,
        string requestFingerprint) =>
        this.Kind == kind &&
        this.PropertyId == propertyId &&
        this.ResourceKind == resourceKind &&
        this.ResourceId == resourceId &&
        this.ExpectedVersion == expectedVersion &&
        string.Equals(
            this.RequestFingerprint,
            requestFingerprint,
            StringComparison.Ordinal);

    public PropertyMutationReceiptDto ToPropertyReceipt()
    {
        if (this.ResultStatus is null ||
            this.ResultProcessingStatus is null)
        {
            throw new InvalidDataException(
                "The property mutation operation has no property receipt.");
        }

        return new(
            this.PropertyId,
            this.ResultStatus.Value,
            this.ResultProcessingStatus.Value,
            this.ResultVersion);
    }

    public RoomMutationReceiptDto ToRoomReceipt()
    {
        if (this.ResultRoomId is null ||
            this.ResultRoomStatus is null)
        {
            throw new InvalidDataException(
                "The property mutation operation has no room receipt.");
        }

        return new(
            this.PropertyId,
            this.ResultRoomId.Value,
            this.ResultRoomStatus.Value,
            this.ResultVersion);
    }

    public BedMutationReceiptDto ToBedReceipt()
    {
        if (this.ResultRoomId is null ||
            this.ResultBedId is null ||
            this.ResultBedStatus is null)
        {
            throw new InvalidDataException(
                "The property mutation operation has no bed receipt.");
        }

        return new(
            this.PropertyId,
            this.ResultRoomId.Value,
            this.ResultBedId.Value,
            this.ResultBedStatus.Value,
            this.ResultVersion,
            this.ResultResourceVersion);
    }

    public BedBatchMutationReceiptDto ToBedBatchReceipt()
    {
        if (this.ResultRoomId is null ||
            this.ResultAffectedBedCount is null)
        {
            throw new InvalidDataException(
                "The property mutation operation has no bed batch receipt.");
        }

        return new(
            this.PropertyId,
            this.ResultRoomId.Value,
            this.ResultAffectedBedCount.Value,
            this.ResultResourceVersion);
    }

    public static PropertyMutationOperationRecord ForProperty(
        Guid operationId,
        string scopeId,
        Guid propertyId,
        PropertyMutationKind kind,
        long expectedVersion,
        string requestFingerprint,
        PropertyMutationReceiptDto receipt,
        DateTimeOffset completedAtUtc) => new(
            operationId,
            scopeId,
            propertyId,
            PropertyMutationResourceKind.Property,
            propertyId,
            kind,
            expectedVersion,
            requestFingerprint,
            receipt.Status,
            receipt.ProcessingStatus,
            null,
            null,
            null,
            null,
            null,
            receipt.Version,
            receipt.Version,
            completedAtUtc);

    public static PropertyMutationOperationRecord ForRoom(
        Guid operationId,
        string scopeId,
        Guid propertyId,
        PropertyMutationResourceKind resourceKind,
        Guid resourceId,
        PropertyMutationKind kind,
        long expectedVersion,
        string requestFingerprint,
        RoomMutationReceiptDto receipt,
        long resultResourceVersion,
        DateTimeOffset completedAtUtc) => new(
            operationId,
            scopeId,
            propertyId,
            resourceKind,
            resourceId,
            kind,
            expectedVersion,
            requestFingerprint,
            null,
            null,
            receipt.RoomId,
            receipt.Status,
            null,
            null,
            null,
            receipt.Version,
            resultResourceVersion,
            completedAtUtc);

    public static PropertyMutationOperationRecord ForBed(
        Guid operationId,
        string scopeId,
        Guid propertyId,
        Guid roomId,
        PropertyMutationKind kind,
        long expectedVersion,
        string requestFingerprint,
        BedMutationReceiptDto receipt,
        DateTimeOffset completedAtUtc) => new(
            operationId,
            scopeId,
            propertyId,
            PropertyMutationResourceKind.Room,
            roomId,
            kind,
            expectedVersion,
            requestFingerprint,
            null,
            null,
            receipt.RoomId,
            null,
            receipt.BedId,
            receipt.Status,
            null,
            receipt.Version,
            receipt.RoomVersion,
            completedAtUtc);

    public static PropertyMutationOperationRecord ForBedBatch(
        Guid operationId,
        string scopeId,
        Guid propertyId,
        Guid roomId,
        PropertyMutationKind kind,
        long expectedVersion,
        string requestFingerprint,
        BedBatchMutationReceiptDto receipt,
        DateTimeOffset completedAtUtc) => new(
            operationId,
            scopeId,
            propertyId,
            PropertyMutationResourceKind.Room,
            roomId,
            kind,
            expectedVersion,
            requestFingerprint,
            null,
            null,
            receipt.RoomId,
            null,
            null,
            null,
            receipt.AffectedBedCount,
            receipt.RoomVersion,
            receipt.RoomVersion,
            completedAtUtc);
}

public enum PropertyMutationResourceKind
{
    Property = 1,
    Room = 2
}

public enum PropertyMutationKind
{
    DetailsUpdate = 1,
    ProcessingActivation = 2,
    ProcessingSuspension = 3,
    Retirement = 4,
    RoomCreate = 5,
    RoomUpdate = 6,
    BedAdd = 7,
    BedBatchAdd = 8,
    BedUpdate = 9
}
