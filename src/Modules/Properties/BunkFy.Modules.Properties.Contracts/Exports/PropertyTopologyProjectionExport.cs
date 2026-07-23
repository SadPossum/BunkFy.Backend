namespace BunkFy.Modules.Properties.Contracts;

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
        long version,
        IReadOnlyCollection<RoomTopologyProjectionExport>? rooms = null,
        PropertyProcessingStatus processingStatus = PropertyProcessingStatus.Unconfigured,
        PropertyGovernancePolicyBinding? governancePolicy = null)
    {
        this.TenantId = TenantIds.Normalize(tenantId);
        this.PropertyId = IntegrationEventContractGuards.RequireId(propertyId, nameof(propertyId));
        this.Name = IntegrationEventContractGuards.NormalizeRequiredText(name, PropertiesContractLimits.PropertyNameMaxLength, nameof(name));
        this.Code = IntegrationEventContractGuards.NormalizeRequiredText(code, PropertiesContractLimits.PropertyCodeMaxLength, nameof(code));
        this.TimeZoneId = IntegrationEventContractGuards.NormalizeRequiredText(timeZoneId, PropertiesContractLimits.TimeZoneIdMaxLength, nameof(timeZoneId));
        this.Status = IntegrationEventContractGuards.NormalizeDefinedOrUnknown(status);
        this.Version = PropertiesEventContractGuards.RequireVersion(version, nameof(version));
        this.Rooms = rooms?.ToArray() ?? [];
        this.ProcessingStatus = processingStatus is
            PropertyProcessingStatus.Unconfigured or
            PropertyProcessingStatus.Enabled or
            PropertyProcessingStatus.Suspended
                ? processingStatus
                : throw new ArgumentOutOfRangeException(nameof(processingStatus));
        if ((processingStatus == PropertyProcessingStatus.Unconfigured && governancePolicy is not null) ||
            (processingStatus is PropertyProcessingStatus.Enabled or PropertyProcessingStatus.Suspended &&
             governancePolicy is null))
        {
            throw new ArgumentException("The processing status and governance policy are inconsistent.");
        }

        this.GovernancePolicy = governancePolicy;
    }

    public string TenantId { get; }
    public Guid PropertyId { get; }
    public string Name { get; }
    public string Code { get; }
    public string TimeZoneId { get; }
    public PropertyStatus Status { get; }
    public long Version { get; }
    public IReadOnlyCollection<RoomTopologyProjectionExport> Rooms { get; }
    public PropertyProcessingStatus ProcessingStatus { get; }
    public PropertyGovernancePolicyBinding? GovernancePolicy { get; }
}
