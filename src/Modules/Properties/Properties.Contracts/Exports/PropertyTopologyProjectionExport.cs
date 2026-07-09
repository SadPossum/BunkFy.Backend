namespace Properties.Contracts;

using Gma.Framework.Messaging;
using Gma.Framework.Naming;

public sealed record PropertyTopologyProjectionExport
{
    public PropertyTopologyProjectionExport(
        string tenantId,
        Guid propertyId,
        string name,
        string code,
        string timeZoneId,
        PropertyStatus status,
        IReadOnlyCollection<RoomTopologyProjectionExport>? rooms = null)
    {
        this.TenantId = TenantIds.Normalize(tenantId);
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Name = IntegrationEventContractGuards.NormalizeRequiredText(name, PropertiesContractLimits.PropertyNameMaxLength, nameof(name));
        this.Code = IntegrationEventContractGuards.NormalizeRequiredText(code, PropertiesContractLimits.PropertyCodeMaxLength, nameof(code));
        this.TimeZoneId = IntegrationEventContractGuards.NormalizeRequiredText(timeZoneId, PropertiesContractLimits.TimeZoneIdMaxLength, nameof(timeZoneId));
        this.Status = IntegrationEventContractGuards.NormalizeDefinedOrUnknown(status);
        this.Rooms = rooms?.ToArray() ?? [];
    }

    public string TenantId { get; }
    public Guid PropertyId { get; }
    public string Name { get; }
    public string Code { get; }
    public string TimeZoneId { get; }
    public PropertyStatus Status { get; }
    public IReadOnlyCollection<RoomTopologyProjectionExport> Rooms { get; }
}
