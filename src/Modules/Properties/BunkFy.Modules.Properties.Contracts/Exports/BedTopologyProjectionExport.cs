namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;

public sealed record BedTopologyProjectionExport
{
    public BedTopologyProjectionExport(
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        string label,
        BedStatus status,
        long version)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.BedId = IntegrationEventContractGuards.RequireId(bedId, nameof(bedId));
        this.Label = IntegrationEventContractGuards.NormalizeRequiredText(label, PropertiesContractLimits.BedLabelMaxLength, nameof(label));
        this.Status = IntegrationEventContractGuards.NormalizeDefinedOrUnknown(status);
        this.Version = PropertiesEventContractGuards.RequireVersion(version, nameof(version));
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
    public string Label { get; }
    public BedStatus Status { get; }
    public long Version { get; }
}
