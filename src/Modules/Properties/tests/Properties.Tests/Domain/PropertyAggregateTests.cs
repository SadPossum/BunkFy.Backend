namespace Properties.Tests;

using Properties.Domain.Aggregates;
using Properties.Domain.Errors;
using Properties.Domain.Events;
using Gma.Framework.Naming;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertyAggregateTests
{
    [Fact]
    public void Create_normalizes_tenant_and_property_code()
    {
        Result<Property> result = CreateProperty(" tenant-a ", code: " HOSTEL-ONE ");

        Assert.True(result.IsSuccess);
        Assert.Equal("tenant-a", result.Value.TenantId);
        Assert.Equal("hostel-one", result.Value.Code.Value);
        PropertyCreatedDomainEvent domainEvent = Assert.IsType<PropertyCreatedDomainEvent>(Assert.Single(result.Value.DomainEvents));
        Assert.Equal("hostel-one", domainEvent.Code);
    }

    [Fact]
    public void Create_rejects_invalid_required_values()
    {
        Assert.Equal(PropertiesDomainErrors.TenantRequired, CreateProperty(" ").Error);
        Assert.Equal(PropertiesDomainErrors.TenantInvalid, CreateProperty(new string('x', TenantIds.MaxLength + 1)).Error);
        Assert.Equal(PropertiesDomainErrors.PropertyIdRequired, CreateProperty("tenant-a", propertyId: Guid.Empty).Error);
        Assert.Equal(PropertiesDomainErrors.DomainEventIdRequired, CreateProperty("tenant-a", eventId: Guid.Empty).Error);
        Assert.Equal(PropertiesDomainErrors.PropertyNameRequired, CreateProperty("tenant-a", name: " ").Error);
        Assert.Equal(PropertiesDomainErrors.PropertyCodeInvalid, CreateProperty("tenant-a", code: "-bad").Error);
        Assert.Equal(PropertiesDomainErrors.TimeZoneInvalid, CreateProperty("tenant-a", timeZoneId: "Invalid/Zone").Error);
    }

    [Fact]
    public void Update_changes_setup_values_and_raises_event()
    {
        Property property = CreateProperty("tenant-a").Value;
        property.ClearDomainEvents();

        Result result = property.Update("Updated", "updated", "UTC", Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated", property.Name.Value);
        Assert.Equal("updated", property.Code.Value);
        Assert.IsType<PropertyUpdatedDomainEvent>(Assert.Single(property.DomainEvents));
    }

    private static Result<Property> CreateProperty(
        string tenantId,
        string name = "Hostel One",
        string code = "hostel-one",
        string timeZoneId = "UTC",
        Guid? propertyId = null,
        Guid? eventId = null) =>
        Property.Create(
            propertyId ?? Guid.NewGuid(),
            tenantId,
            name,
            code,
            timeZoneId,
            eventId ?? Guid.NewGuid(),
            DateTimeOffset.UtcNow);
}
