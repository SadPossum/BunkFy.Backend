namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationOperationLockConfiguration
    : IEntityTypeConfiguration<ReservationOperationLock>
{
    public void Configure(EntityTypeBuilder<ReservationOperationLock> builder)
    {
        builder.ToTable("reservation_operation_locks", table =>
        {
            table.HasCheckConstraint(
                "CK_reservation_operation_locks_revision",
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
            resourceLock.ReservationId
        }).IsUnique();
    }
}
