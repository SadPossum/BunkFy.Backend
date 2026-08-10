namespace BunkFy.Modules.Properties.Persistence.Configurations;

using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class PropertiesTenantRevisionConfiguration
    : IEntityTypeConfiguration<PropertiesTenantRevision>
{
    public void Configure(
        EntityTypeBuilder<PropertiesTenantRevision> builder)
    {
        builder.ToTable("tenant_revisions", table =>
        {
            table.HasCheckConstraint(
                "CK_properties_tenant_revision_positive",
                "\"Revision\" > 0");
            table.HasCheckConstraint(
                "CK_properties_tenant_revision_lifecycle",
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
            .HasMaxLength(ScopeIds.MaxLength)
            .ValueGeneratedNever();
        builder.Property(revision => revision.Revision)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(revision => revision.LifecycleStatus)
            .HasConversion<int>()
            .HasDefaultValue(PropertiesTenantLifecycleStatus.Open)
            .HasSentinel(default)
            .IsRequired();
        builder.Property(revision => revision.DestroyRequestSha256)
            .HasMaxLength(64)
            .IsFixedLength();
    }
}
