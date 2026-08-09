namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class InventoryManagementOperationConfiguration
    : IEntityTypeConfiguration<InventoryManagementOperation>
{
    private const string EmptyGuid =
        "00000000-0000-0000-0000-000000000000";

    public void Configure(
        EntityTypeBuilder<InventoryManagementOperation> builder)
    {
        builder.ToTable("management_operations", table =>
        {
            table.HasCheckConstraint(
                "CK_inventory_management_operations_coordinates",
                $"\"Id\" <> '{EmptyGuid}' AND " +
                $"\"PropertyId\" <> '{EmptyGuid}' AND " +
                $"\"ResourceId\" <> '{EmptyGuid}' AND " +
                "char_length(btrim(\"ScopeId\")) > 0");
            table.HasCheckConstraint(
                "CK_inventory_management_operations_fingerprint",
                "char_length(\"RequestFingerprint\") = 64 AND " +
                "\"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_inventory_management_operations_kind",
                "\"Kind\" BETWEEN 1 AND 5 AND " +
                "((\"Kind\" = 1 AND \"ResourceKind\" = 1) OR " +
                "(\"Kind\" IN (2, 3) AND \"ResourceKind\" = 2 AND " +
                "\"ResourceId\" = \"PropertyId\") OR " +
                "(\"Kind\" = 4 AND \"ResourceKind\" = 3 AND " +
                "\"ResultBlockId\" IS NOT NULL AND " +
                "\"ResourceId\" = \"ResultBlockId\") OR " +
                "(\"Kind\" = 5 AND \"ResourceKind\" = 4 AND " +
                "\"ResultBlockGroupId\" IS NOT NULL AND " +
                "\"ResourceId\" = \"ResultBlockGroupId\"))");
            table.HasCheckConstraint(
                "CK_inventory_management_operations_result",
                "(\"Kind\" = 1 AND \"ExpectedVersion\" > 0 AND " +
                "\"ResultVersion\" BETWEEN \"ExpectedVersion\" AND " +
                "\"ExpectedVersion\" + 1 AND " +
                "\"ResultSalesMode\" IS NOT NULL AND " +
                "\"ResultSalesMode\" IN (2, 3) AND " +
                "\"ResultBlockId\" IS NULL AND " +
                "\"ResultBlockGroupId\" IS NULL AND " +
                "\"ResultBlockStatus\" IS NULL AND " +
                "\"ResultAffectedBlockCount\" IS NULL) OR " +
                "(\"Kind\" = 2 AND \"ExpectedVersion\" = 0 AND " +
                "\"ResultVersion\" = 1 AND " +
                "\"ResultSalesMode\" IS NULL AND " +
                "\"ResultBlockId\" IS NOT NULL AND " +
                $"\"ResultBlockId\" <> '{EmptyGuid}' AND " +
                "\"ResultBlockGroupId\" IS NOT NULL AND " +
                $"\"ResultBlockGroupId\" <> '{EmptyGuid}' AND " +
                "\"ResultBlockStatus\" IS NOT NULL AND " +
                "\"ResultBlockStatus\" = 1 AND " +
                "\"ResultAffectedBlockCount\" IS NOT NULL AND " +
                "\"ResultAffectedBlockCount\" = 1) OR " +
                "(\"Kind\" = 3 AND \"ExpectedVersion\" = 0 AND " +
                "\"ResultVersion\" = 0 AND " +
                "\"ResultSalesMode\" IS NULL AND " +
                "\"ResultBlockId\" IS NULL AND " +
                "\"ResultBlockGroupId\" IS NOT NULL AND " +
                $"\"ResultBlockGroupId\" <> '{EmptyGuid}' AND " +
                "\"ResultBlockStatus\" IS NULL AND " +
                "\"ResultAffectedBlockCount\" IS NOT NULL AND " +
                "\"ResultAffectedBlockCount\" > 0) OR " +
                "(\"Kind\" = 4 AND \"ExpectedVersion\" > 0 AND " +
                "\"ResultVersion\" = \"ExpectedVersion\" + 1 AND " +
                "\"ResultSalesMode\" IS NULL AND " +
                "\"ResultBlockId\" IS NOT NULL AND " +
                $"\"ResultBlockId\" <> '{EmptyGuid}' AND " +
                "\"ResultBlockGroupId\" IS NOT NULL AND " +
                $"\"ResultBlockGroupId\" <> '{EmptyGuid}' AND " +
                "\"ResultBlockStatus\" IS NOT NULL AND " +
                "\"ResultBlockStatus\" = 2 AND " +
                "\"ResultAffectedBlockCount\" IS NOT NULL AND " +
                "\"ResultAffectedBlockCount\" = 1) OR " +
                "(\"Kind\" = 5 AND \"ExpectedVersion\" = 0 AND " +
                "\"ResultVersion\" = 0 AND " +
                "\"ResultSalesMode\" IS NULL AND " +
                "\"ResultBlockId\" IS NULL AND " +
                "\"ResultBlockGroupId\" IS NOT NULL AND " +
                $"\"ResultBlockGroupId\" <> '{EmptyGuid}' AND " +
                "\"ResultBlockStatus\" IS NULL AND " +
                "\"ResultAffectedBlockCount\" IS NOT NULL AND " +
                "\"ResultAffectedBlockCount\" > 0)");
        });
        builder.HasKey(operation => new
        {
            operation.ScopeId,
            operation.ResourceKind,
            operation.ResourceId,
            operation.Id
        });
        builder.Property(operation => operation.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(operation => operation.ResourceKind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(operation => operation.Kind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(operation => operation.RequestFingerprint)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(operation => operation.ResultSalesMode)
            .HasConversion<int?>();
        builder.Property(operation => operation.ResultBlockStatus)
            .HasConversion<int?>();
        builder.HasIndex(operation => new
        {
            operation.ScopeId,
            operation.PropertyId,
            operation.CompletedAtUtc,
            operation.ResourceKind,
            operation.ResourceId,
            operation.Id
        });
    }
}
