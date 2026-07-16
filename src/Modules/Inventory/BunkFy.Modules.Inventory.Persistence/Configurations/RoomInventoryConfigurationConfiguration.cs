namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class RoomInventoryConfigurationConfiguration : IEntityTypeConfiguration<RoomInventoryConfiguration>
{
    public void Configure(EntityTypeBuilder<RoomInventoryConfiguration> builder)
    {
        builder.ToTable("room_configurations");
        builder.HasKey(configuration => configuration.Id);
        builder.Property(configuration => configuration.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(configuration => configuration.SalesMode).HasConversion<int>();
        builder.Property(configuration => configuration.Version).IsConcurrencyToken();
        builder.Property(configuration => configuration.AvailabilityMutationVersion).IsConcurrencyToken().IsRequired();
        builder.HasIndex(configuration => new { configuration.ScopeId, configuration.PropertyId });
        builder.Ignore(configuration => configuration.DomainEvents);
    }
}
