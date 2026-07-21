namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Domain;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceStaffAccessPlanTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Lifecycle_is_idempotent_and_superseded_plans_cannot_reactivate()
    {
        Guid sourceId = Guid.NewGuid();
        Guid profileId = Guid.NewGuid();
        Guid propertyA = Guid.NewGuid();
        Guid propertyB = Guid.NewGuid();
        WorkspaceStaffAccessPlan plan = WorkspaceStaffAccessPlan.Create(
            sourceId,
            Guid.NewGuid().ToString("D"),
            WorkspaceStaffOnboardingSource.Invitation,
            profileId,
            "front-desk",
            [propertyB, propertyA, propertyA],
            "owner-a",
            Now).Value;

        Assert.Equal(WorkspaceStaffAccessPlanState.Prepared, plan.Status);
        Assert.Equal(1, plan.Version);
        Assert.Equal(
            new[] { propertyA, propertyB }.Order(),
            plan.Properties.Select(item => item.PropertyId));

        Assert.True(plan.Activate(Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(WorkspaceStaffAccessPlanState.Active, plan.Status);
        Assert.Equal(2, plan.Version);

        Assert.True(plan.Activate(Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(2, plan.Version);

        Assert.True(plan.Supersede(Now.AddMinutes(3)).IsSuccess);
        Assert.Equal(WorkspaceStaffAccessPlanState.Superseded, plan.Status);
        Assert.Equal(3, plan.Version);

        Assert.True(plan.Supersede(Now.AddMinutes(4)).IsSuccess);
        Assert.Equal(3, plan.Version);
        Assert.True(plan.Activate(Now.AddMinutes(5)).IsFailure);
    }

    [Fact]
    public void Replay_matching_is_exact_but_property_order_is_irrelevant()
    {
        Guid profileId = Guid.NewGuid();
        Guid propertyA = Guid.NewGuid();
        Guid propertyB = Guid.NewGuid();
        WorkspaceStaffAccessPlan plan = WorkspaceStaffAccessPlan.Create(
            Guid.NewGuid(),
            Guid.NewGuid().ToString("D"),
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            profileId,
            "housekeeping",
            [propertyA, propertyB],
            "manager-a",
            Now).Value;

        Assert.True(plan.Matches(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            profileId,
            "housekeeping",
            [propertyB, propertyA],
            "manager-a"));
        Assert.False(plan.Matches(
            WorkspaceStaffOnboardingSource.Invitation,
            profileId,
            "housekeeping",
            [propertyA, propertyB],
            "manager-a"));
        Assert.False(plan.Matches(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            profileId,
            "housekeeping",
            [propertyA],
            "manager-a"));
        Assert.False(plan.Matches(
            WorkspaceStaffOnboardingSource.EnrollmentLink,
            profileId,
            "housekeeping",
            [propertyA, propertyB],
            "another-manager"));
    }
}
