namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Persistence.TenantTermination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionTenantDestroyReceiptConfiguration
    : IEntityTypeConfiguration<IngestionTenantDestroyReceipt>
{
    public void Configure(
        EntityTypeBuilder<IngestionTenantDestroyReceipt> builder)
    {
        builder.ToTable("tenant_destroy_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_ingestion_tenant_destroy_receipt_revisions",
                "\"SelectedRevision\" >= 0 AND " +
                "\"ResultingRevision\" = \"SelectedRevision\" + 1");
            table.HasCheckConstraint(
                "CK_ingestion_tenant_destroy_receipt_progress",
                "((\"RemovedRecordCount\" = 0 AND " +
                "\"CompletedBatchCount\" = 0) OR " +
                "(\"RemovedRecordCount\" > 0 AND " +
                "\"CompletedBatchCount\" > 0)) AND " +
                "((\"RemovedRawPayloadCount\" = 0 AND " +
                "\"CompletedRawPayloadBatchCount\" = 0) OR " +
                "(\"RemovedRawPayloadCount\" > 0 AND " +
                "\"CompletedRawPayloadBatchCount\" > 0)) AND " +
                $"\"BatchSize\" BETWEEN 1 AND " +
                $"{IngestionTenantDestroyOperation.MaximumBatchSize} AND " +
                "\"RemovalProofVersion\" = 1 AND " +
                "\"RawPayloadRemovalProofVersion\" = 1");
            table.HasCheckConstraint(
                "CK_ingestion_tenant_destroy_receipt_times",
                "\"CompletedAtUtc\" >= \"StartedAtUtc\"");
        });
        builder.HasKey(receipt => receipt.OperationId);
        builder.Property(receipt => receipt.OperationId)
            .ValueGeneratedNever();
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.RequestSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.RemovalProofSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.RawPayloadRemovalProofSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Ignore(receipt => receipt.RemovedArtifactCount);
        builder.HasIndex(receipt => receipt.ScopeId).IsUnique();
    }
}
