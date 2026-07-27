namespace BunkFy.Modules.Retention.Persistence;

using BunkFy.Modules.Retention.Domain.Aggregates;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

public sealed class RetentionDbContext(
    DbContextOptions<RetentionDbContext> options,
    IScopeContext scopeContext)
    : ScopeAwareDbContext<RetentionDbContext>(options, scopeContext)
{
    public DbSet<RetentionExecution> Executions => this.Set<RetentionExecution>();
    public DbSet<RetentionTenantProjection> TenantProjections =>
        this.Set<RetentionTenantProjection>();
    public DbSet<RetentionPropertyProjection> PropertyProjections =>
        this.Set<RetentionPropertyProjection>();
    public DbSet<RetentionScheduleState> ScheduleStates =>
        this.Set<RetentionScheduleState>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(RetentionMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(RetentionDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}
