namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DataRightsRestoreCheckpointConfiguration
    : IEntityTypeConfiguration<DataRightsRestoreCheckpoint>
{
    public void Configure(
        EntityTypeBuilder<DataRightsRestoreCheckpoint> builder)
    {
        builder.ToTable("restore_checkpoints", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_restore_checkpoint_sequence",
                "\"TenantSequence\" >= 0 AND \"Version\" >= 0");
            table.HasCheckConstraint(
                "CK_data_rights_restore_checkpoint_digests",
                $"char_length(\"EntrySha256\") = {DataRightsProcessingLedgerEntry.Sha256Length} AND " +
                $"char_length(\"StorageMacSha256\") = {DataRightsProcessingLedgerEntry.Sha256Length} AND " +
                $"char_length(\"CheckpointMacSha256\") = {DataRightsProcessingLedgerEntry.Sha256Length} AND " +
                $"char_length(\"ScopeSnapshotSha256\") = {DataRightsProcessingLedgerEntry.Sha256Length}");
            table.HasCheckConstraint(
                "CK_data_rights_restore_checkpoint_confirmation",
                "\"IntegrityKeyVersion\" >= 0 AND \"LastReconciledAtUtc\" <> '-infinity'");
        });

        builder.HasKey(checkpoint => checkpoint.Id);
        builder.Property(checkpoint => checkpoint.Id).ValueGeneratedNever();
        builder.Property(checkpoint => checkpoint.EntrySha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(checkpoint => checkpoint.StorageMacSha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(checkpoint => checkpoint.CheckpointMacSha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(checkpoint => checkpoint.ScopeSnapshotSha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(checkpoint => checkpoint.Version)
            .IsConcurrencyToken();
        builder.HasIndex(checkpoint => checkpoint.ScopeId).IsUnique();
    }
}
