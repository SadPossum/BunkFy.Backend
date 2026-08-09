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
                "\"ResourceKind\" = 1 AND \"Kind\" = 1");
            table.HasCheckConstraint(
                "CK_inventory_management_operations_result",
                "\"ExpectedVersion\" > 0 AND " +
                "\"ResultVersion\" >= \"ExpectedVersion\" AND " +
                "\"ResultVersion\" - \"ExpectedVersion\" <= 1 AND " +
                "\"ResultSalesMode\" IN (2, 3)");
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
            .HasConversion<int>()
            .IsRequired();
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
