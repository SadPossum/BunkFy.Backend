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

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => WorkspaceStaffOnboardingTests.OrganizationId.ToString("D");
    }
}
