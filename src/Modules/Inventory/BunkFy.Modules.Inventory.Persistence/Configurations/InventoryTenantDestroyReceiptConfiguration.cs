namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using BunkFy.Modules.Inventory.Persistence.TenantTermination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class InventoryTenantDestroyReceiptConfiguration
    : IEntityTypeConfiguration<InventoryTenantDestroyReceipt>
{
    public void Configure(
        EntityTypeBuilder<InventoryTenantDestroyReceipt> builder)
    {
        builder.ToTable("tenant_destroy_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_inventory_tenant_destroy_receipt_revisions",
                "\"SelectedRevision\" >= 0 AND " +
                "\"ResultingRevision\" = \"SelectedRevision\" + 1");
            table.HasCheckConstraint(
                "CK_inventory_tenant_destroy_receipt_progress",
                "((\"RemovedRecordCount\" = 0 AND " +
                "\"CompletedBatchCount\" = 0) OR " +
                "(\"RemovedRecordCount\" > 0 AND " +
                "\"CompletedBatchCount\" > 0)) AND " +
                $"\"BatchSize\" BETWEEN 1 AND " +
                $"{InventoryTenantDestroyOperation.MaximumBatchSize} AND " +
                "\"RemovalProofVersion\" = 1");
            table.HasCheckConstraint(
                "CK_inventory_tenant_destroy_receipt_times",
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
        builder.HasIndex(receipt => receipt.ScopeId).IsUnique();
    }
}
