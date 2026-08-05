namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Persistence.TenantTermination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionTenantRevisionConfiguration
    : IEntityTypeConfiguration<IngestionTenantRevision>
{
    public void Configure(EntityTypeBuilder<IngestionTenantRevision> builder)
    {
        builder.ToTable(
            "tenant_revisions",
            IngestionMigrations.Schema,
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ingestion_tenant_revision_positive",
                    "\"Revision\" > 0");
                table.HasCheckConstraint(
                    "CK_ingestion_tenant_revision_lifecycle",
                    "(\"LifecycleStatus\" = 1 AND " +
                    "\"DestroyOperationId\" IS NULL AND " +
                    "\"DestroyRequestSha256\" IS NULL AND " +
                    "\"DestroyStartedAtUtc\" IS NULL AND " +
                    "\"DestroyCompletedAtUtc\" IS NULL) OR " +
                    "(\"LifecycleStatus\" = 2 AND " +
                    "\"DestroyOperationId\" IS NOT NULL AND " +
                    "\"DestroyRequestSha256\" IS NOT NULL AND " +
                    "\"DestroyStartedAtUtc\" IS NOT NULL AND " +
                    "\"DestroyCompletedAtUtc\" IS NULL) OR " +
                    "(\"LifecycleStatus\" = 3 AND " +
                    "\"DestroyOperationId\" IS NOT NULL AND " +
                    "\"DestroyRequestSha256\" IS NOT NULL AND " +
                    "\"DestroyStartedAtUtc\" IS NOT NULL AND " +
                    "\"DestroyCompletedAtUtc\" >= " +
                    "\"DestroyStartedAtUtc\")");
            });
        builder.HasKey(revision => revision.ScopeId);
        builder.Property(revision => revision.ScopeId)
            .HasMaxLength(128)
            .ValueGeneratedNever();
        builder.Property(revision => revision.Revision)
            .IsConcurrencyToken();
        builder.Property(revision => revision.LifecycleStatus)
            .HasConversion<int>()
            .HasDefaultValue(IngestionTenantLifecycleStatus.Open)
            .IsRequired();
        builder.Property(revision => revision.DestroyRequestSha256)
            .HasMaxLength(64)
            .IsFixedLength();
    }
}
