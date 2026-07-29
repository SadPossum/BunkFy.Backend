namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Retention;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestRetentionSweepCheckpointConfiguration
    : IEntityTypeConfiguration<GuestRetentionSweepCheckpoint>
{
    public void Configure(
        EntityTypeBuilder<GuestRetentionSweepCheckpoint> builder)
    {
        builder.ToTable("guest_retention_sweep_checkpoints", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_retention_sweep_checkpoints_cursor",
                "\"AfterProjectionOrdinal\" >= 0");
            table.HasCheckConstraint(
                "CK_guest_retention_sweep_checkpoints_key",
                "length(trim(\"DataClassKey\")) > 0");
            table.HasCheckConstraint(
                "CK_guest_retention_sweep_checkpoints_version",
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
            .HasMaxLength(GuestRetentionExecution.DataClassKeyMaxLength)
            .IsRequired();
        builder.Property(checkpoint => checkpoint.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(checkpoint => new
        {
            checkpoint.ScopeId,
            checkpoint.DataClassKey
        }).IsUnique();
        builder.Ignore(checkpoint => checkpoint.DomainEvents);
    }
}
