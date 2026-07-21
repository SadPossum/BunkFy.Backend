namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspacesModelTests
{
    [Fact]
    public void Staff_onboarding_is_tenant_filtered_and_concurrency_protected()
    {
        DbContextOptions<WorkspacesDbContext> options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using WorkspacesDbContext context = new(options, new TestScopeContext());
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity = context.Model
            .FindEntityType(typeof(WorkspaceStaffOnboarding))!;

        Assert.NotNull(entity.FindProperty(nameof(WorkspaceStaffOnboarding.Version))?.IsConcurrencyToken);
        Assert.True(entity.FindProperty(nameof(WorkspaceStaffOnboarding.Version))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffOnboarding.ScopeId),
                nameof(WorkspaceStaffOnboarding.SourceKind),
                nameof(WorkspaceStaffOnboarding.SourceId),
                nameof(WorkspaceStaffOnboarding.SubjectId)]));
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Staff_access_process_is_tenant_filtered_versioned_and_staff_version_unique()
    {
        DbContextOptions<WorkspacesDbContext> options = new DbContextOptionsBuilder<WorkspacesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        using WorkspacesDbContext context = new(options, new TestScopeContext());
        Microsoft.EntityFrameworkCore.Metadata.IEntityType entity = context.Model
            .FindEntityType(typeof(WorkspaceStaffAccessProcess))!;

        Assert.True(entity.FindProperty(nameof(WorkspaceStaffAccessProcess.Version))!.IsConcurrencyToken);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(WorkspaceStaffAccessProcess.ScopeId),
                nameof(WorkspaceStaffAccessProcess.StaffMemberId),
                nameof(WorkspaceStaffAccessProcess.TargetStaffVersion)]));
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());

        Microsoft.EntityFrameworkCore.Metadata.IEntityType snapshot = context.Model
            .FindEntityType(typeof(WorkspaceStaffAccessProfileSnapshot))!;
        Assert.Equal(
            [
                "ProcessId",
                nameof(WorkspaceStaffAccessProfileSnapshot.ProfileId),
                nameof(WorkspaceStaffAccessProfileSnapshot.AssignmentScope)
            ],
            snapshot.FindPrimaryKey()!.Properties.Select(property => property.Name).ToArray());
        Assert.Equal(
            WorkspaceStaffAccessProfileSnapshot.AssignmentScopeMaxLength,
            snapshot.FindProperty(nameof(WorkspaceStaffAccessProfileSnapshot.AssignmentScope))!
                .GetMaxLength());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => WorkspaceStaffOnboardingTests.OrganizationId.ToString("D");
    }
}
