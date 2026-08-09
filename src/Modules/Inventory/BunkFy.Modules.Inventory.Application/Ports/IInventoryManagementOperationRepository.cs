namespace BunkFy.Modules.Inventory.Application.Ports;

using BunkFy.Modules.Inventory.Contracts;

public interface IInventoryManagementOperationRepository
{
    Task<InventoryManagementOperationRecord?> GetAsync(
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        Guid operationId,
        CancellationToken cancellationToken);

    Task AddAsync(
        InventoryManagementOperationRecord operation,
        CancellationToken cancellationToken);
}

public sealed record InventoryManagementOperationRecord(
    Guid OperationId,
    string ScopeId,
    Guid PropertyId,
    InventoryManagementResourceKind ResourceKind,
    Guid ResourceId,
    InventoryManagementMutationKind Kind,
    long ExpectedVersion,
    string RequestFingerprint,
    InventorySalesMode ResultSalesMode,
    long ResultVersion,
    DateTimeOffset CompletedAtUtc)
{
    public bool Matches(
        InventoryManagementMutationKind kind,
        Guid propertyId,
        InventoryManagementResourceKind resourceKind,
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

    public RoomInventoryMutationReceiptDto ToRoomReceipt() => new(
        this.PropertyId,
        this.ResourceId,
        this.ResultSalesMode,
        this.ResultVersion);
}

public enum InventoryManagementResourceKind
{
    Room = 1
}

public enum InventoryManagementMutationKind
{
    RoomSalesModeConfiguration = 1
}
