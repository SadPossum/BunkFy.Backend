namespace Properties.Persistence;

using Properties.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Tenancy;

public sealed class PropertiesDbContext(DbContextOptions<PropertiesDbContext> options, ITenantContext tenantContext)
    : TenantAwareDbContext<PropertiesDbContext>(options, tenantContext)
{
    public DbSet<Property> Properties => this.Set<Property>();
    public DbSet<Room> Rooms => this.Set<Room>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(PropertiesMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PropertiesDbContext).Assembly);
        this.ApplyTenantConventions(modelBuilder);
    }
}
