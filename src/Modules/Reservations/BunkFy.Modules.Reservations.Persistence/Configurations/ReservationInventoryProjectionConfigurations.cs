namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationInventoryUnitProjectionConfiguration
    : IEntityTypeConfiguration<ReservationInventoryUnitProjection>
{
    public void Configure(EntityTypeBuilder<ReservationInventoryUnitProjection> builder)
    {
        builder.ToTable("inventory_unit_projections");
        builder.HasKey(unit => unit.Id);
        builder.HasAlternateKey(unit => new { unit.ScopeId, unit.Id });
        builder.Property(unit => unit.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(unit => unit.Kind).HasConversion<int>().IsRequired();
        builder.Property(unit => unit.Label).HasMaxLength(200).IsRequired();
        builder.Property(unit => unit.UnitVersion).IsConcurrencyToken().IsRequired();
        builder.HasIndex(unit => new { unit.ScopeId, unit.PropertyId, unit.RoomId, unit.IsSellable });
    }
}

internal sealed class ReservationInventoryBlockProjectionConfiguration
    : IEntityTypeConfiguration<ReservationInventoryBlockProjection>
{
    public void Configure(EntityTypeBuilder<ReservationInventoryBlockProjection> builder)
    {
        builder.ToTable("inventory_block_projections");
        builder.HasKey(block => block.Id);
        builder.HasAlternateKey(block => new { block.ScopeId, block.Id });
        builder.Property(block => block.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(block => block.Status).HasConversion<int>().IsRequired();
        builder.HasIndex(block => new
        {
            block.ScopeId,
            block.PropertyId,
            block.InventoryUnitId,
            block.Status,
            block.Arrival,
            block.Departure
        });
    }
}

internal sealed class ReservationInventoryAllocationProjectionConfiguration
    : IEntityTypeConfiguration<ReservationInventoryAllocationProjection>
{
    public void Configure(EntityTypeBuilder<ReservationInventoryAllocationProjection> builder)
    {
        builder.ToTable("inventory_allocation_projections");
        builder.HasKey(allocation => allocation.Id);
        builder.HasAlternateKey(allocation => new { allocation.ScopeId, allocation.Id });
        builder.Property(allocation => allocation.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(allocation => allocation.Status).HasConversion<int>().IsRequired();
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
    }
}

internal sealed class ReservationInventoryAllocationUnitProjectionConfiguration
    : IEntityTypeConfiguration<ReservationInventoryAllocationUnitProjection>
{
    public void Configure(EntityTypeBuilder<ReservationInventoryAllocationUnitProjection> builder)
    {
        builder.ToTable("inventory_allocation_unit_projections");
        builder.HasKey(unit => new { unit.ScopeId, unit.AllocationId, unit.Id });
        builder.Property(unit => unit.ScopeId).HasMaxLength(128).IsRequired();
        builder.Ignore(unit => unit.InventoryUnitId);
        builder.HasIndex(unit => new { unit.ScopeId, unit.Id, unit.AllocationId });
    }
}
