namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Persistence.TenantTermination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionTenantDestroyOperationConfiguration
    : IEntityTypeConfiguration<IngestionTenantDestroyOperation>
{
    public void Configure(
        EntityTypeBuilder<IngestionTenantDestroyOperation> builder)
    {
        builder.ToTable("tenant_destroy_operations", table =>
        {
            table.HasCheckConstraint(
                "CK_ingestion_tenant_destroy_operation_revisions",
                "\"SelectedRevision\" >= 0 AND " +
                "\"ResultingRevision\" = \"SelectedRevision\" + 1");
            table.HasCheckConstraint(
                "CK_ingestion_tenant_destroy_operation_batch",
                $"\"BatchSize\" BETWEEN 1 AND " +
                $"{IngestionTenantDestroyOperation.MaximumBatchSize}");
            table.HasCheckConstraint(
                "CK_ingestion_tenant_destroy_operation_progress",
                $"\"Stage\" BETWEEN 1 AND " +
                $"{(int)IngestionTenantDestroyStage.Completed} AND " +
                "\"RemovedRecordCount\" >= 0 AND " +
                "\"CompletedBatchCount\" >= 0 AND " +
                "\"RemovedRawPayloadCount\" >= 0 AND " +
                "\"CompletedRawPayloadBatchCount\" >= 0 AND " +
                "\"ProofVersion\" = 1 AND " +
                "\"RawPayloadRemovalProofVersion\" = 1 AND " +
                "\"ConcurrencyVersion\" >= 1");
            table.HasCheckConstraint(
                "CK_ingestion_tenant_destroy_operation_times",
                "\"UpdatedAtUtc\" >= \"StartedAtUtc\"");
        });
        builder.HasKey(operation => operation.OperationId);
        builder.Property(operation => operation.OperationId)
            .ValueGeneratedNever();
        builder.Property(operation => operation.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(operation => operation.RequestSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(operation => operation.Stage)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(operation => operation.RemovalProofSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(operation => operation.RawPayloadRemovalProofSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(operation => operation.ConcurrencyVersion)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(operation => operation.ScopeId).IsUnique();
    }
}
