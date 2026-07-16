namespace BunkFy.Modules.Inventory.Persistence;

using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Gma.Framework.Scoping;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options, IScopeContext scopeContext)
    : ScopeAwareDbContext<InventoryDbContext>(options, scopeContext)
{
    public DbSet<InventoryPropertyTopology> PropertyTopology => this.Set<InventoryPropertyTopology>();
    public DbSet<InventoryRoomTopology> RoomTopology => this.Set<InventoryRoomTopology>();
    public DbSet<InventoryBedTopology> BedTopology => this.Set<InventoryBedTopology>();
    public DbSet<InventoryUnit> InventoryUnits => this.Set<InventoryUnit>();
    public DbSet<RoomInventoryConfiguration> RoomConfigurations => this.Set<RoomInventoryConfiguration>();
    public DbSet<ManualInventoryBlock> ManualBlocks => this.Set<ManualInventoryBlock>();
    public DbSet<InventoryAllocation> Allocations => this.Set<InventoryAllocation>();
    public DbSet<InventoryAllocationUnit> AllocationUnits => this.Set<InventoryAllocationUnit>();
    public DbSet<InventoryAllocationAmendmentDecision> AllocationAmendmentDecisions => this.Set<InventoryAllocationAmendmentDecision>();
    public DbSet<BedRetirementProcess> BedRetirements => this.Set<BedRetirementProcess>();
    public DbSet<RoomRetirementProcess> RoomRetirements => this.Set<RoomRetirementProcess>();
    public DbSet<OutboxMessage> OutboxMessages => this.Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => this.Set<InboxMessage>();
    public DbSet<InventoryProjectionRebuildCheckpoint> ProjectionRebuildCheckpoints => this.Set<InventoryProjectionRebuildCheckpoint>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(InventoryMigrations.Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
        this.ApplyScopeConventions(modelBuilder);
    }
}
