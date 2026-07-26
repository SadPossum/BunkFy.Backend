namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionAnonymisationReceiptConfiguration
    : IEntityTypeConfiguration<IngestionAnonymisationReceipt>
{
    public void Configure(
        EntityTypeBuilder<IngestionAnonymisationReceipt> builder)
    {
        builder.ToTable("anonymisation_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_receipts_contract",
                $"\"ContractVersion\" = " +
                IngestionAnonymisationReceipt
                    .CurrentContractVersion);
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_receipts_versions",
                "\"SelectedSourceLinkVersion\" >= 1 AND " +
                "\"ResultingSourceLinkVersion\" = " +
                "\"SelectedSourceLinkVersion\" + 1");
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_receipts_outcome",
                "\"Disposition\" = 1 AND \"Reason\" = 1");
            table.HasCheckConstraint(
                "CK_ingestion_anonymisation_receipts_counts",
                "\"GraphRecordCount\" >= 1 AND " +
                "\"FingerprintCount\" >= 1 AND " +
                "\"RawPayloadCount\" >= 0");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.Id
        });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(
                IngestionAnonymisationReceipt.ActorIdMaxLength)
            .IsRequired();
        builder.Property(receipt =>
                receipt.ApprovalEvidenceSha256)
            .HasMaxLength(
                IngestionAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt =>
                receipt.PolicyEvidenceSha256)
            .HasMaxLength(
                IngestionAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt =>
                receipt.OperationFenceSha256)
            .HasMaxLength(
                IngestionAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(
                IngestionAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.WorkItemId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.SourceLinkId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.CompletedAtUtc
        });
        builder.HasOne<IngestionAnonymisationTombstone>()
            .WithOne()
            .HasForeignKey<IngestionAnonymisationReceipt>(
                receipt => new
                {
                    receipt.ScopeId,
                    receipt.SourceLinkId
                })
            .HasPrincipalKey<IngestionAnonymisationTombstone>(
                tombstone => new
                {
                    tombstone.ScopeId,
                    tombstone.Id
                })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
