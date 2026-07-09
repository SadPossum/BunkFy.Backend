namespace Properties.Contracts;

using Gma.Framework.Messaging;

public sealed record BedTopologyProjectionExport
{
    public BedTopologyProjectionExport(
        Guid propertyId,
        Guid roomId,
        Guid bedId,
        string label,
        BedStatus status)
    {
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.RoomId = IntegrationEventContractGuards.RequireId(roomId, nameof(roomId));
        this.BedId = IntegrationEventContractGuards.RequireId(bedId, nameof(bedId));
        this.Label = IntegrationEventContractGuards.NormalizeRequiredText(label, PropertiesContractLimits.BedLabelMaxLength, nameof(label));
        this.Status = IntegrationEventContractGuards.NormalizeDefinedOrUnknown(status);
    }

    public Guid PropertyId { get; }
    public Guid RoomId { get; }
    public Guid BedId { get; }
    public string Label { get; }
    public BedStatus Status { get; }
}
