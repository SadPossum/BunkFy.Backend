namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class BedRetirementProcessConfiguration : IEntityTypeConfiguration<BedRetirementProcess>
{
    public void Configure(EntityTypeBuilder<BedRetirementProcess> builder)
    {
        builder.ToTable("bed_retirements");
        builder.HasKey(process => process.Id);
        builder.Property(process => process.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(process => process.Reason).HasMaxLength(BedRetirementProcess.ReasonMaxLength).IsRequired();
        builder.Property(process => process.RequestedBy).HasMaxLength(BedRetirementProcess.ActorIdMaxLength).IsRequired();
        builder.Property(process => process.State).HasConversion<int>().IsRequired();
        builder.Property(process => process.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(process => new { process.ScopeId, process.BedId }).IsUnique();
        builder.HasIndex(process => new { process.ScopeId, process.PropertyId, process.RoomId, process.State });
        builder.Ignore(process => process.DomainEvents);
    }
}
