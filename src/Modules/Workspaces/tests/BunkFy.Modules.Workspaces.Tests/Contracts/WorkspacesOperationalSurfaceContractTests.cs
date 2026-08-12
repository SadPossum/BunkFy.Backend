namespace BunkFy.Modules.Workspaces.Tests.Contracts;

using BunkFy.Modules.Workspaces.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspacesOperationalSurfaceContractTests
{
    [Fact]
    public void Actionable_onboarding_summary_has_no_staged_profile_or_subject_coordinates()
    {
        string[] members = typeof(WorkspaceStaffOnboardingActionableSummaryDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Contains(
            nameof(WorkspaceStaffOnboardingActionableSummaryDto.HasStaffTarget),
            members);
        foreach (string forbidden in new[]
                 {
                     "SubjectId",
                     "VerifiedAccountEmail",
                     "DisplayName",
                     "LegalName",
                     "WorkEmail",
                     "WorkPhone",
                     "EmployeeNumber",
                     "JobTitle",
                     "Department",
                     "StaffMemberId"
                 })
        {
            Assert.DoesNotContain(forbidden, members);
        }
    }

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
