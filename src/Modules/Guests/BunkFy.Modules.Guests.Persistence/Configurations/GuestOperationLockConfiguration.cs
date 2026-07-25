namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestOperationLockConfiguration
    : IEntityTypeConfiguration<GuestOperationLock>
{
    public void Configure(EntityTypeBuilder<GuestOperationLock> builder)
    {
        builder.ToTable("guest_operation_locks", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_operation_locks_resource_kind",
                "\"ResourceKind\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_guest_operation_locks_revision",
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
        builder.Property(resourceLock => resourceLock.ResourceKind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(resourceLock => resourceLock.Revision)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(resourceLock => new
        {
            resourceLock.ScopeId,
            resourceLock.ResourceKind,
            resourceLock.ResourceId
        }).IsUnique();
    }
}
