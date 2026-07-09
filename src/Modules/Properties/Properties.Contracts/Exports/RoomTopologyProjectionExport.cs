namespace Properties.Contracts;

using Gma.Framework.Messaging;

public sealed record RoomTopologyProjectionExport
{
    public RoomTopologyProjectionExport(
        Guid propertyId,
        Guid roomId,
        string name,
        string? buildingLabel,
        string? floorLabel,
        RoomStatus status,
        IReadOnlyCollection<BedTopologyProjectionExport>? beds = null)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.Name = IntegrationEventContractGuards.NormalizeRequiredText(name, PropertiesContractLimits.RoomNameMaxLength, nameof(name));
        this.BuildingLabel = PropertiesEventContractGuards.NormalizeOptionalLabel(buildingLabel, nameof(buildingLabel));
        this.FloorLabel = PropertiesEventContractGuards.NormalizeOptionalLabel(floorLabel, nameof(floorLabel));
        this.Status = IntegrationEventContractGuards.NormalizeDefinedOrUnknown(status);
        this.Beds = beds?.ToArray() ?? [];
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public string Name { get; }
    public string? BuildingLabel { get; }
    public string? FloorLabel { get; }
    public RoomStatus Status { get; }
    public IReadOnlyCollection<BedTopologyProjectionExport> Beds { get; }
}
