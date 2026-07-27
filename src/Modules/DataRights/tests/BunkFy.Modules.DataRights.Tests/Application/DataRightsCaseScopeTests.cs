namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsCaseScopeTests
{
    [Fact]
    public void Property_scope_requires_a_real_property_id()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DataRightsCaseScope.ForProperty(Guid.Empty));
    }

    [Fact]
    public void Tenant_scopes_preserve_the_exact_case_kind()
    {
        Assert.True(DataRightsCaseScope.Staff.IsTenant);
        Assert.Equal(DataRightsCaseType.StaffRights, DataRightsCaseScope.Staff.CaseType);
        Assert.Null(DataRightsCaseScope.Staff.PropertyId);

        Assert.True(DataRightsCaseScope.TenantTermination.IsTenant);
        Assert.Equal(
            DataRightsCaseType.TenantTermination,
            DataRightsCaseScope.TenantTermination.CaseType);
        Assert.Null(DataRightsCaseScope.TenantTermination.PropertyId);
        Assert.NotEqual(
            DataRightsCaseScope.Staff.CaseType,
            DataRightsCaseScope.TenantTermination.CaseType);
    }
}
