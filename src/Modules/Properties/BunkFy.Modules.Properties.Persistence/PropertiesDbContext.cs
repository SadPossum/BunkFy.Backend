namespace BunkFy.Modules.Properties.Persistence;

using BunkFy.Modules.Properties.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;

public sealed class PropertiesDbContext(DbContextOptions<PropertiesDbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<PropertiesDbContext>(options, scopeContext)
{
    public DbSet<Property> Properties => this.Set<Property>();
    public DbSet<Room> Rooms => this.Set<Room>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PropertiesMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PropertiesDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}
