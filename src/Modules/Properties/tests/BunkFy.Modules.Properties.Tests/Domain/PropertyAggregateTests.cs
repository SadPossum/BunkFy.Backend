namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.Events;
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
        Assert.Equal("tenant-a", result.Value.ScopeId);
        Assert.Equal("hostel-one", result.Value.Code.Value);
        Assert.Equal(1, result.Value.Version);
        PropertyCreatedDomainEvent domainEvent = Assert.IsType<PropertyCreatedDomainEvent>(Assert.Single(result.Value.DomainEvents));
        Assert.Equal("hostel-one", domainEvent.Code);
        Assert.Equal(1, domainEvent.PropertyVersion);
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

        Result result = property.Update("Updated", "updated", "UTC", property.Version, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated", property.Name.Value);
        Assert.Equal("updated", property.Code.Value);
        Assert.Equal(2, property.Version);
        PropertyUpdatedDomainEvent domainEvent = Assert.IsType<PropertyUpdatedDomainEvent>(Assert.Single(property.DomainEvents));
        Assert.Equal(2, domainEvent.PropertyVersion);
    }

    [Fact]
    public void Stale_version_is_rejected_without_mutation()
    {
        Property property = CreateProperty("tenant-a").Value;

        Result result = property.Update("Updated", "updated", "UTC", expectedVersion: 99, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(PropertiesDomainErrors.VersionConflict, result.Error);
        Assert.Equal(1, property.Version);
        Assert.Equal("Hostel One", property.Name.Value);
    }

    [Fact]
    public void Retire_is_terminal_and_versioned()
    {
        Property property = CreateProperty("tenant-a").Value;
        property.ClearDomainEvents();

        Result result = property.Retire(property.Version, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(PropertyState.Retired, property.Status);
        Assert.Equal(2, property.Version);
        Assert.NotNull(property.RetiredAtUtc);
        PropertyRetiredDomainEvent domainEvent = Assert.IsType<PropertyRetiredDomainEvent>(Assert.Single(property.DomainEvents));
        Assert.Equal(2, domainEvent.PropertyVersion);
        Assert.Equal(PropertiesDomainErrors.PropertyRetired, property.RegisterRoom(property.Version).Error);
        Assert.Equal(
            PropertiesDomainErrors.PropertyRetired,
            property.Update("Nope", "nope", "UTC", property.Version, Guid.NewGuid(), DateTimeOffset.UtcNow).Error);
        Assert.Equal(
            PropertiesDomainErrors.PropertyAlreadyRetired,
            property.Retire(property.Version, Guid.NewGuid(), DateTimeOffset.UtcNow).Error);
    }

    [Fact]
    public void Retire_rejects_invalid_actor_without_mutating_the_property()
    {
        Property property = CreateProperty("tenant-a").Value;
        property.ClearDomainEvents();

        Result result = property.Retire(
            property.Version,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            new string('a', Property.ActorIdMaxLength + 1));

        Assert.Equal(PropertiesDomainErrors.ActorIdInvalid, result.Error);
        Assert.Equal(PropertyState.Active, property.Status);
        Assert.Equal(1, property.Version);
        Assert.Null(property.RetiredAtUtc);
        Assert.Empty(property.DomainEvents);
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
