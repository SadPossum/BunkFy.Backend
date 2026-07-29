namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using BunkFy.Modules.Reservations.Domain.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationRetentionSweepCheckpointConfiguration
    : IEntityTypeConfiguration<ReservationRetentionSweepCheckpoint>
{
    public void Configure(
        EntityTypeBuilder<ReservationRetentionSweepCheckpoint> builder)
    {
        builder.ToTable(
            "reservation_retention_sweep_checkpoints",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_reservation_retention_checkpoints_cursor",
                    "\"AfterProjectionOrdinal\" >= 0");
                table.HasCheckConstraint(
                    "CK_reservation_retention_checkpoints_key",
                    "\"DataClassKey\" ~ '^[A-Za-z0-9.-]+$'");
                table.HasCheckConstraint(
                    "CK_reservation_retention_checkpoints_policy",
                    "\"ExecutionPolicyVersion\" >= 1");
                table.HasCheckConstraint(
                    "CK_reservation_retention_checkpoints_version",
                    "\"Version\" >= 1");
            });
        builder.HasKey(checkpoint => checkpoint.Id);
        builder.HasAlternateKey(checkpoint => new
        {
            checkpoint.ScopeId,
            checkpoint.Id
        });
        builder.Property(checkpoint => checkpoint.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(checkpoint => checkpoint.DataClassKey)
            .HasMaxLength(
                ReservationRetentionExecution.DataClassKeyMaxLength)
            .IsRequired();
        builder.Property(checkpoint => checkpoint.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(checkpoint => new
        {
            checkpoint.ScopeId,
            checkpoint.DataClassKey,
            checkpoint.ExecutionPolicyVersion
        }).IsUnique();
        builder.Ignore(checkpoint => checkpoint.DomainEvents);
    }
}
