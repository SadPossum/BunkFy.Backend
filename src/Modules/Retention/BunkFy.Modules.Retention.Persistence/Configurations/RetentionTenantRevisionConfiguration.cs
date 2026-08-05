namespace BunkFy.Modules.Retention.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class RetentionTenantRevisionConfiguration
    : IEntityTypeConfiguration<RetentionTenantRevision>
{
    public void Configure(
        EntityTypeBuilder<RetentionTenantRevision> builder)
    {
        builder.ToTable("tenant_revisions", table =>
        {
            table.HasCheckConstraint(
                "CK_retention_tenant_revision_positive",
                "\"Revision\" > 0");
            table.HasCheckConstraint(
                "CK_retention_tenant_revision_lifecycle",
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
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(revision => revision.LifecycleStatus)
            .HasConversion<int>()
            .HasDefaultValue(RetentionTenantLifecycleStatus.Open)
            .IsRequired();
        builder.Property(revision => revision.DestroyRequestSha256)
            .HasMaxLength(64)
            .IsFixedLength();
    }
}
