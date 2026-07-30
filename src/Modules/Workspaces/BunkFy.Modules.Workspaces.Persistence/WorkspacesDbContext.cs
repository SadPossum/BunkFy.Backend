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
    public DbSet<WorkspaceStaffAccessProcess> StaffAccessProcesses =>
        this.Set<WorkspaceStaffAccessProcess>();
    public DbSet<WorkspaceStaffAccessPlan> StaffAccessPlans =>
        this.Set<WorkspaceStaffAccessPlan>();
    public DbSet<WorkspaceStaffRetentionCorrelationReceipt>
        StaffRetentionCorrelationReceipts =>
        this.Set<WorkspaceStaffRetentionCorrelationReceipt>();
    public DbSet<WorkspacePropertyProjection> PropertyProjections =>
        this.Set<WorkspacePropertyProjection>();
    public DbSet<WorkspaceProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints =>
        this.Set<WorkspaceProjectionRebuildCheckpoint>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.EnsureReceiptsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        this.EnsureReceiptsAreAppendOnly();
        return base.SaveChangesAsync(
            acceptAllChangesOnSuccess,
            cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(WorkspacesMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkspacesDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }

    private void EnsureReceiptsAreAppendOnly()
    {
        bool mutationRequested = this.ChangeTracker
            .Entries<WorkspaceStaffRetentionCorrelationReceipt>()
            .Any(entry =>
                entry.State is
                    EntityState.Modified or EntityState.Deleted);
        if (mutationRequested)
        {
            throw new InvalidOperationException(
                "Workspace immutable receipts are append-only.");
        }
    }
}
