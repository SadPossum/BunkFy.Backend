namespace BunkFy.Modules.Workspaces.Tests.Contracts;

using BunkFy.Modules.Workspaces.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspacesOperationalSurfaceContractTests
{
    [Theory]
    [InlineData(typeof(WorkspaceStaffOnboardingListResponse))]
    [InlineData(typeof(WorkspaceStaffAccessProcessListResponse))]
    [InlineData(typeof(WorkspaceStaffJoinSourceListResponse))]
    public void Workspaces_owned_operational_directories_report_continuation(Type responseType)
    {
        System.Reflection.PropertyInfo property = Assert.Single(
            responseType.GetProperties(),
            candidate => string.Equals(
                candidate.Name,
                "HasMore",
                StringComparison.Ordinal));

        Assert.Equal(typeof(bool), property.PropertyType);
    }
}
