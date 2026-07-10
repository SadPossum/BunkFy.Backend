namespace Inventory.Application.Ports;

using Inventory.Contracts;
using Properties.Contracts;

public sealed record InventoryPropertyTopologyWriteModel(
    string ScopeId,
    Guid PropertyId,
    string? Name,
    string? Code,
    string? TimeZoneId,
    PropertyStatus Status,
    long SourceVersion);

public sealed record InventoryRoomTopologyWriteModel(
    string ScopeId,
    Guid PropertyId,
    Guid RoomId,
    string? Name,
    string? BuildingLabel,
    string? FloorLabel,
    RoomStatus Status,
    long SourceVersion);

public sealed record InventoryBedTopologyWriteModel(
    string ScopeId,
    Guid PropertyId,
    Guid RoomId,
    Guid BedId,
    string? Label,
    BedStatus Status,
    long BedSourceVersion);

public sealed record InventoryRoomTopologySnapshot(
    Guid PropertyId,
    Guid RoomId,
    RoomStatus Status,
    int ActiveBedCount);

public sealed record InventoryUnitDefinitionSnapshot(
    string ScopeId,
    Guid InventoryUnitId,
    Guid PropertyId,
    Guid RoomId,
    Guid? BedId,
    InventoryUnitKind Kind,
    string Label,
    bool IsTopologyActive,
    bool IsSellable,
    long ConfigurationVersion,
    long UnitVersion);
