namespace BunkFy.Modules.Staff.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Entities;

public sealed class StaffDbContext(DbContextOptions<StaffDbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<StaffDbContext>(options, scopeContext)
{
    public DbSet<StaffMember> StaffMembers => this.Set<StaffMember>();
    public DbSet<StaffPropertyAssignment> PropertyAssignments => this.Set<StaffPropertyAssignment>();
    public DbSet<StaffPropertyProjection> PropertyProjections => this.Set<StaffPropertyProjection>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    public DbSet<StaffProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints =>
        this.Set<StaffProjectionRebuildCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(StaffMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StaffDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}
