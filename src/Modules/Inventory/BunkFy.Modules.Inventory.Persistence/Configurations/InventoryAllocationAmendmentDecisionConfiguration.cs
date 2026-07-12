namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class InventoryAllocationAmendmentDecisionConfiguration
    : IEntityTypeConfiguration<InventoryAllocationAmendmentDecision>
{
    public void Configure(EntityTypeBuilder<InventoryAllocationAmendmentDecision> builder)
    {
        builder.ToTable("allocation_amendment_decisions");
        builder.HasKey(decision => decision.Id);
        builder.Property(decision => decision.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(decision => decision.RequestFingerprint).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(decision => decision.RejectionReason).HasConversion<int?>();
        builder.HasIndex(decision => new { decision.ScopeId, decision.AllocationId, decision.DecidedAtUtc });
    }
}
