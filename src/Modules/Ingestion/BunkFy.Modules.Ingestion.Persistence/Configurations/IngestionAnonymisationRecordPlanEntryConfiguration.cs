namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionAnonymisationRecordPlanEntryConfiguration
    : IEntityTypeConfiguration<IngestionAnonymisationRecordPlanEntry>
{
    public void Configure(
        EntityTypeBuilder<IngestionAnonymisationRecordPlanEntry> builder)
    {
        builder.ToTable("anonymisation_record_plan", table =>
        {
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_record_plan_kind",
                "\"Kind\" IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_record_plan_versions",
                "\"SelectedVersion\" >= 1 AND " +
                "\"ReductionVersion\" = \"SelectedVersion\" + " +
                "CASE WHEN \"Kind\" = 5 THEN 0 ELSE 1 END AND " +
                "\"ResultingVersion\" = \"ReductionVersion\" + " +
                "CASE WHEN \"RawPayloadFileId\" IS NULL THEN 0 ELSE 1 END");
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_record_plan_raw_payload",
                "(\"RawPayloadFileId\" IS NULL AND " +
                "\"RawPayloadConnectionId\" IS NULL) OR " +
                "(\"Kind\" = 2 AND \"RawPayloadFileId\" IS NOT NULL AND " +
                "\"RawPayloadConnectionId\" IS NOT NULL)");
        });
        builder.HasKey(entry => entry.Id);
        builder.HasAlternateKey(entry => new
        {
            entry.ScopeId,
            entry.Id
        });
        builder.Property(entry => entry.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.HasIndex(entry => new
        {
            entry.ScopeId,
            entry.TombstoneId,
            entry.Kind,
            entry.RecordId
        }).IsUnique();
        builder.HasIndex(entry => new
        {
            entry.ScopeId,
            entry.TombstoneId,
            entry.RawPayloadFileId
        });
        builder.HasOne<IngestionAnonymisationTombstone>()
            .WithMany()
            .HasForeignKey(entry => new
            {
                entry.ScopeId,
                entry.TombstoneId
            })
            .HasPrincipalKey(tombstone => new
            {
                tombstone.ScopeId,
                tombstone.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
