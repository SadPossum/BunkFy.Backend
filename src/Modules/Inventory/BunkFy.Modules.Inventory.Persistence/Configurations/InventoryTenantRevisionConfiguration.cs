namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using Gma.Framework.Naming;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class InventoryTenantRevisionConfiguration
    : IEntityTypeConfiguration<InventoryTenantRevision>
{
    public void Configure(
        EntityTypeBuilder<InventoryTenantRevision> builder)
    {
        builder.ToTable("tenant_revisions", table =>
        {
            table.HasCheckConstraint(
                "CK_inventory_tenant_revision_positive",
                "\"Revision\" > 0");
            table.HasCheckConstraint(
                "CK_inventory_tenant_revision_lifecycle",
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
            .HasDefaultValue(InventoryTenantLifecycleStatus.Open)
            .HasSentinel(default)
            .IsRequired();
        builder.Property(revision => revision.DestroyRequestSha256)
            .HasMaxLength(64)
            .IsFixedLength();
    }
}
