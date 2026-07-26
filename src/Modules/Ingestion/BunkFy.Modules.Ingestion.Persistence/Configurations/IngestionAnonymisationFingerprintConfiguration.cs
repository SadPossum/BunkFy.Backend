namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionAnonymisationFingerprintConfiguration
    : IEntityTypeConfiguration<IngestionAnonymisationFingerprint>
{
    public void Configure(
        EntityTypeBuilder<IngestionAnonymisationFingerprint> builder)
    {
        builder.ToTable("anonymisation_fingerprints", table =>
        {
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_fingerprints_purpose",
                "\"Purpose\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_fingerprints_key_version",
                "\"KeyVersion\" >= 1");
        });
        builder.HasKey(fingerprint => fingerprint.Id);
        builder.HasAlternateKey(fingerprint => new
        {
            fingerprint.ScopeId,
            fingerprint.Id
        });
        builder.Property(fingerprint => fingerprint.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(fingerprint => fingerprint.Sha256)
            .HasMaxLength(IngestionAnonymisationFingerprint.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(fingerprint => new
        {
            fingerprint.ScopeId,
            fingerprint.Purpose,
            fingerprint.KeyVersion,
            fingerprint.Sha256
        }).IsUnique();
        builder.HasIndex(fingerprint => new
        {
            fingerprint.ScopeId,
            fingerprint.TombstoneId
        });
        builder.HasOne<IngestionAnonymisationTombstone>()
            .WithMany()
            .HasForeignKey(fingerprint => new
            {
                fingerprint.ScopeId,
                fingerprint.TombstoneId
            })
            .HasPrincipalKey(tombstone => new
            {
                tombstone.ScopeId,
                tombstone.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
