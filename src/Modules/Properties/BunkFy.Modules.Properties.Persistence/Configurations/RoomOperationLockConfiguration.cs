namespace BunkFy.Modules.Properties.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class RoomOperationLockConfiguration
    : IEntityTypeConfiguration<RoomOperationLock>
{
    public void Configure(EntityTypeBuilder<RoomOperationLock> builder)
    {
        builder.ToTable("room_operation_locks", table =>
        {
            table.HasCheckConstraint(
                "CK_room_operation_locks_coordinate",
                "\"Id\" = \"RoomId\"");
            table.HasCheckConstraint(
                "CK_room_operation_locks_revision",
                "\"Revision\" >= 1");
        });
        builder.HasKey(resourceLock => resourceLock.Id);
        builder.HasAlternateKey(resourceLock => new
        {
            resourceLock.ScopeId,
            resourceLock.Id
        });
        builder.Property(resourceLock => resourceLock.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(resourceLock => resourceLock.Revision)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(resourceLock => new
        {
            resourceLock.ScopeId,
            resourceLock.RoomId
        }).IsUnique();
    }
}
