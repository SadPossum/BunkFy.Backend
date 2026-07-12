namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class InventoryAllocationConfiguration : IEntityTypeConfiguration<InventoryAllocation>
{
    public void Configure(EntityTypeBuilder<InventoryAllocation> builder)
    {
        builder.ToTable("allocations");
        builder.HasKey(allocation => allocation.Id);
        builder.HasAlternateKey(allocation => new { allocation.ScopeId, allocation.Id });
        builder.Property(allocation => allocation.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(allocation => allocation.Status).HasConversion<int>().IsRequired();
        builder.Property(allocation => allocation.Rejection).HasConversion<int>().IsRequired();
        builder.Property(allocation => allocation.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(allocation => new { allocation.ScopeId, allocation.AllocationRequestId }).IsUnique();
        builder.HasIndex(allocation => new { allocation.ScopeId, allocation.ReservationId }).IsUnique();
        builder.HasIndex(allocation => new
        {
            allocation.ScopeId,
            allocation.PropertyId,
            allocation.Status,
            allocation.Arrival,
            allocation.Departure
        });
        builder.HasMany(allocation => allocation.Units)
            .WithOne()
            .HasForeignKey(unit => new { unit.ScopeId, unit.AllocationId })
            .HasPrincipalKey(allocation => new { allocation.ScopeId, allocation.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(allocation => allocation.Units).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(allocation => allocation.DomainEvents);
    }
}

internal sealed class InventoryAllocationUnitConfiguration : IEntityTypeConfiguration<InventoryAllocationUnit>
{
    public void Configure(EntityTypeBuilder<InventoryAllocationUnit> builder)
    {
        builder.ToTable("allocation_units");
        builder.HasKey(unit => new { unit.ScopeId, unit.AllocationId, unit.Id });
        builder.Property(unit => unit.ScopeId).HasMaxLength(128).IsRequired();
        builder.Ignore(unit => unit.InventoryUnitId);
        builder.HasIndex(unit => new { unit.ScopeId, unit.Id, unit.AllocationId });
    }
}
