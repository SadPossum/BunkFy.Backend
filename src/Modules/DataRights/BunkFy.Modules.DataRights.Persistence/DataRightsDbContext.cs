namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

public sealed class DataRightsDbContext(
    DbContextOptions<DataRightsDbContext> options,
    IScopeContext scopeContext) : ScopeAwareDbContext<DataRightsDbContext>(options, scopeContext)
{
    public DbSet<DataRightsCase> Cases => this.Set<DataRightsCase>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DataRightsMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DataRightsDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}
