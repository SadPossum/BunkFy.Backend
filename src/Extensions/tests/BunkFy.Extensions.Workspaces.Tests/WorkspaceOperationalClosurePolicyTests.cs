namespace BunkFy.Extensions.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Organizations.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceOperationalClosurePolicyTests
{
    private static readonly Guid WorkspaceId =
        Guid.Parse("970ce7c3-45ce-4ca8-b920-71e7db6c8388");
    private static readonly AccessScope WorkspaceScope =
        WorkspaceAccessScopes.Create(WorkspaceId.ToString("D"));

    [Theory]
    [InlineData(
        WorkspaceOperationalAdmissionOutcome.Allowed,
        OrganizationMutationAdmissionDecision.Allowed)]
    [InlineData(
        WorkspaceOperationalAdmissionOutcome.Restricted,
        OrganizationMutationAdmissionDecision.Denied)]
    [InlineData(
        WorkspaceOperationalAdmissionOutcome.Unavailable,
        OrganizationMutationAdmissionDecision.Unavailable)]
    [InlineData(
        WorkspaceOperationalAdmissionOutcome.Unknown,
        OrganizationMutationAdmissionDecision.Unavailable)]
    public async Task Organization_mutation_policy_maps_operational_admission(
        WorkspaceOperationalAdmissionOutcome outcome,
        OrganizationMutationAdmissionDecision expected)
    {
        StubWorkspaceOperationalAdmissionPolicy admission = new(outcome);
        WorkspaceOrganizationMutationAdmissionPolicy policy = new(admission);

        OrganizationMutationAdmissionDecision actual =
            await policy.EvaluateAsync(new OrganizationMutationAdmissionContext(
                OrganizationMutationAdmissionOperation.UpdateOrganization,
                WorkspaceId,
                "owner-a"));

        Assert.Equal(expected, actual);
        Assert.Equal([WorkspaceId.ToString("D")], admission.TenantIds);
    }

    [Theory]
    [InlineData(
        WorkspaceOperationalAdmissionOutcome.Allowed,
        AccessProfileMutationAdmissionDecision.Allowed)]
    [InlineData(
        WorkspaceOperationalAdmissionOutcome.Restricted,
        AccessProfileMutationAdmissionDecision.Denied)]
    [InlineData(
        WorkspaceOperationalAdmissionOutcome.Unavailable,
        AccessProfileMutationAdmissionDecision.Unavailable)]
    [InlineData(
        WorkspaceOperationalAdmissionOutcome.Unknown,
        AccessProfileMutationAdmissionDecision.Unavailable)]
    public async Task Profile_mutation_policy_maps_operational_admission_once(
        WorkspaceOperationalAdmissionOutcome outcome,
        AccessProfileMutationAdmissionDecision expected)
    {
        StubWorkspaceOperationalAdmissionPolicy admission = new(outcome);
        WorkspaceAccessProfileMutationAdmissionPolicy policy =
            new(admission);

        AccessProfileMutationAdmissionDecision actual =
            await policy.EvaluateAsync(
                new AccessProfileMutationAdmissionContext(
                    AccessProfileMutationAdmissionOperation.CreateProfile,
                    WorkspaceScope,
                    AccessSubject.User("owner-a"),
                    ProfileKey: "front-desk"));

        Assert.Equal(expected, actual);
        Assert.Equal([WorkspaceId.ToString("D")], admission.TenantIds);
    }

    [Fact]
    public async Task Profile_mutation_outside_workspace_is_left_to_other_policies()
    {
        StubWorkspaceOperationalAdmissionPolicy admission = new(
            WorkspaceOperationalAdmissionOutcome.Restricted);
        WorkspaceAccessProfileMutationAdmissionPolicy policy =
            new(admission);

        AccessProfileMutationAdmissionDecision actual =
            await policy.EvaluateAsync(
                new AccessProfileMutationAdmissionContext(
                    AccessProfileMutationAdmissionOperation.CreateProfile,
                    AccessScope.Global,
                    AccessSubject.User("owner-a"),
                    ProfileKey: "front-desk"));

        Assert.Equal(
            AccessProfileMutationAdmissionDecision.Allowed,
            actual);
        Assert.Empty(admission.TenantIds);
    }

    [Theory]
    [InlineData(WorkspaceOperationalAdmissionOutcome.Allowed, true)]
    [InlineData(WorkspaceOperationalAdmissionOutcome.Restricted, false)]
    [InlineData(WorkspaceOperationalAdmissionOutcome.Unavailable, false)]
    [InlineData(WorkspaceOperationalAdmissionOutcome.Unknown, false)]
    public async Task Role_assignment_requires_open_operational_admission(
        WorkspaceOperationalAdmissionOutcome outcome,
        bool expected)
    {
        StubWorkspaceOperationalAdmissionPolicy admission = new(outcome);
        WorkspaceOperationalRoleAssignmentPolicy policy = new(admission);

        bool actual = await policy.IsAllowedAsync(
            new AccessRoleAssignmentPolicyContext(
                AccessSubject.User("member-a"),
                WorkspaceAccessRoles.MembershipMarker,
                WorkspaceScope,
                expiresAtUtc: null),
            CancellationToken.None);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task Role_assignment_outside_workspace_is_left_to_other_policies()
    {
        StubWorkspaceOperationalAdmissionPolicy admission = new(
            WorkspaceOperationalAdmissionOutcome.Restricted);
        WorkspaceOperationalRoleAssignmentPolicy policy = new(admission);

        bool allowed = await policy.IsAllowedAsync(
            new AccessRoleAssignmentPolicyContext(
                AccessSubject.User("member-a"),
                "other.role",
                AccessScope.Global,
                expiresAtUtc: null),
            CancellationToken.None);

        Assert.True(allowed);
        Assert.Empty(admission.TenantIds);
    }

    [Fact]
    public void Workspace_registration_binds_every_operational_closure_adapter()
    {
        ServiceCollection services = new();

        services.AddBunkFyWorkspaces();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IOrganizationMutationAdmissionPolicy) &&
            descriptor.ImplementationType ==
                typeof(WorkspaceOrganizationMutationAdmissionPolicy));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType ==
                typeof(IAccessProfileMutationAdmissionPolicy) &&
            descriptor.ImplementationType ==
                typeof(WorkspaceAccessProfileMutationAdmissionPolicy));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IAccessRoleAssignmentPolicy) &&
            descriptor.ImplementationType ==
                typeof(WorkspaceOperationalRoleAssignmentPolicy));
    }

}
