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
    InventorySalesMode? ResultSalesMode,
    Guid? ResultBlockId,
    Guid? ResultBlockGroupId,
    ManualInventoryBlockStatus? ResultBlockStatus,
    int? ResultAffectedBlockCount,
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

    public RoomInventoryMutationReceiptDto ToRoomReceipt()
    {
        if (this.ResultSalesMode is null)
        {
            throw new InvalidDataException(
                "The Inventory management operation has no Room receipt.");
        }

        return new(
            this.PropertyId,
            this.ResourceId,
            this.ResultSalesMode.Value,
            this.ResultVersion);
    }

    public ManualInventoryBlockMutationReceiptDto ToBlockReceipt()
    {
        if (this.ResultBlockId is null ||
            this.ResultBlockGroupId is null ||
            this.ResultBlockStatus is null)
        {
            throw new InvalidDataException(
                "The Inventory management operation has no Block receipt.");
        }

        return new(
            this.ResultBlockId.Value,
            this.ResultBlockGroupId.Value,
            this.PropertyId,
            this.ResultBlockStatus.Value,
            this.ResultVersion);
    }

    public ManualInventoryBlockGroupMutationReceiptDto ToBlockGroupReceipt()
    {
        if (this.ResultBlockGroupId is null ||
            this.ResultAffectedBlockCount is null)
        {
            throw new InvalidDataException(
                "The Inventory management operation has no Block Group receipt.");
        }

        return new(
            this.ResultBlockGroupId.Value,
            this.PropertyId,
            this.ResultAffectedBlockCount.Value);
    }

    public static InventoryManagementOperationRecord ForRoom(
        Guid operationId,
        string scopeId,
        Guid propertyId,
        Guid roomId,
        long expectedVersion,
        string requestFingerprint,
        RoomInventoryMutationReceiptDto receipt,
        DateTimeOffset completedAtUtc) => new(
            operationId,
            scopeId,
            propertyId,
            InventoryManagementResourceKind.Room,
            roomId,
            InventoryManagementMutationKind.RoomSalesModeConfiguration,
            expectedVersion,
            requestFingerprint,
            receipt.SalesMode,
            null,
            null,
            null,
            null,
            receipt.Version,
            completedAtUtc);

    public static InventoryManagementOperationRecord ForBlock(
        Guid operationId,
        string scopeId,
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        InventoryManagementMutationKind kind,
        long expectedVersion,
        string requestFingerprint,
        ManualInventoryBlockMutationReceiptDto receipt,
        DateTimeOffset completedAtUtc) => new(
            operationId,
            scopeId,
            receipt.PropertyId,
            resourceKind,
            resourceId,
            kind,
            expectedVersion,
            requestFingerprint,
            null,
            receipt.BlockId,
            receipt.BlockGroupId,
            receipt.Status,
            1,
            receipt.Version,
            completedAtUtc);

    public static InventoryManagementOperationRecord ForBlockGroup(
        Guid operationId,
        string scopeId,
        InventoryManagementResourceKind resourceKind,
        Guid resourceId,
        InventoryManagementMutationKind kind,
        string requestFingerprint,
        ManualInventoryBlockGroupMutationReceiptDto receipt,
        DateTimeOffset completedAtUtc) => new(
            operationId,
            scopeId,
            receipt.PropertyId,
            resourceKind,
            resourceId,
            kind,
            0,
            requestFingerprint,
            null,
            null,
            receipt.BlockGroupId,
            null,
            receipt.AffectedBlockCount,
            0,
            completedAtUtc);
}

public enum InventoryManagementResourceKind
{
    Room = 1,
    Property = 2,
    Block = 3,
    BlockGroup = 4
}

public enum InventoryManagementMutationKind
{
    RoomSalesModeConfiguration = 1,
    ManualBlockCreate = 2,
    ManualBlockGroupCreate = 3,
    ManualBlockRelease = 4,
    ManualBlockGroupRelease = 5
}
