namespace BunkFy.Modules.Workspaces.Persistence;

using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

public sealed class WorkspacesDbContext(
    DbContextOptions<WorkspacesDbContext> options,
    IScopeContext scopeContext)
    : ScopeAwareDbContext<WorkspacesDbContext>(options, scopeContext)
{
    public DbSet<WorkspaceStaffOnboarding> StaffOnboardingApplications =>
        this.Set<WorkspaceStaffOnboarding>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(WorkspacesMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkspacesDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}
