namespace BunkFy.Modules.DataRights.Tests.Domain;

using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsExecutionScopeTests
{
    [Fact]
    public void Factories_create_only_supported_case_scope_pairs()
    {
        Guid propertyId = Guid.NewGuid();

        DataRightsExecutionScope property =
            DataRightsExecutionScope.ForProperty(propertyId);

        Assert.True(property.Matches(
            DataRightsCaseKind.GuestRights,
            DataRightsCaseScopeKind.Property,
            propertyId));
        Assert.True(DataRightsExecutionScope.Staff.Matches(
            DataRightsCaseKind.StaffRights,
            DataRightsCaseScopeKind.Tenant,
            propertyId: null));
        Assert.True(DataRightsExecutionScope.TenantTermination.Matches(
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseScopeKind.Tenant,
            propertyId: null));
    }

    [Fact]
    public void Creation_rejects_mixed_case_scope_coordinates()
    {
        Assert.True(DataRightsExecutionScope.Create(
            DataRightsCaseKind.GuestRights,
            DataRightsCaseScopeKind.Tenant,
            propertyId: null).IsFailure);
        Assert.True(DataRightsExecutionScope.Create(
            DataRightsCaseKind.StaffRights,
            DataRightsCaseScopeKind.Property,
            Guid.NewGuid()).IsFailure);
        Assert.True(DataRightsExecutionScope.Create(
            DataRightsCaseKind.TenantTermination,
            DataRightsCaseScopeKind.Tenant,
            Guid.NewGuid()).IsFailure);
    }
}
