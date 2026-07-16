namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class RoomRetirementProcessConfiguration : IEntityTypeConfiguration<RoomRetirementProcess>
{
    public void Configure(EntityTypeBuilder<RoomRetirementProcess> builder)
    {
        builder.ToTable("room_retirements");
        builder.HasKey(process => process.Id);
        builder.Property(process => process.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(process => process.Reason).HasMaxLength(RoomRetirementProcess.ReasonMaxLength).IsRequired();
        builder.Property(process => process.RequestedBy).HasMaxLength(RoomRetirementProcess.ActorIdMaxLength).IsRequired();
        builder.Property(process => process.State).HasConversion<int>().IsRequired();
        builder.Property(process => process.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(process => new { process.ScopeId, process.RoomId }).IsUnique();
        builder.HasIndex(process => new { process.ScopeId, process.PropertyId, process.State });
        builder.Ignore(process => process.DomainEvents);
    }
}
