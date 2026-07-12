namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ManualInventoryBlockConfiguration : IEntityTypeConfiguration<ManualInventoryBlock>
{
    public void Configure(EntityTypeBuilder<ManualInventoryBlock> builder)
    {
        builder.ToTable("manual_blocks");
        builder.HasKey(block => block.Id);
        builder.Property(block => block.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(block => block.Reason).HasMaxLength(ManualInventoryBlock.ReasonMaxLength).IsRequired();
        builder.Property(block => block.Status).HasConversion<int>().IsRequired();
        builder.Property(block => block.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(block => new
        {
            block.ScopeId,
            block.PropertyId,
            block.InventoryUnitId,
            block.Status,
            block.Arrival,
            block.Departure
        });
        builder.HasOne<InventoryUnit>()
            .WithMany()
            .HasForeignKey(block => new { block.ScopeId, block.InventoryUnitId })
            .HasPrincipalKey(unit => new { unit.ScopeId, unit.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(block => block.DomainEvents);
    }
}
