namespace BunkFy.Modules.Properties.Persistence.Configurations;

using BunkFy.Modules.Properties.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class PropertyMutationOperationConfiguration
    : IEntityTypeConfiguration<PropertyMutationOperation>
{
    public void Configure(
        EntityTypeBuilder<PropertyMutationOperation> builder)
    {
        builder.ToTable("property_mutation_operations", table =>
        {
            table.HasCheckConstraint(
                "CK_properties_property_mutation_operations_versions",
                "\"ExpectedVersion\" > 0 AND " +
                "\"ResultVersion\" > 0 AND " +
                "\"ResultResourceVersion\" >= \"ExpectedVersion\" AND " +
                "\"ResultResourceVersion\" <= \"ExpectedVersion\" + 1");
            table.HasCheckConstraint(
                "CK_properties_property_mutation_operations_fingerprint",
                "char_length(\"RequestFingerprint\") = 64 AND " +
                "\"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_properties_property_mutation_operations_status",
                "(\"Kind\" IN (1, 2, 3, 4) AND " +
                "\"ResultStatus\" IN (1, 2) AND " +
                "\"ResultProcessingStatus\" IN (1, 2, 3) AND " +
                "\"ResultRoomId\" IS NULL AND " +
                "\"ResultRoomStatus\" IS NULL) OR " +
                "(\"Kind\" IN (5, 6) AND " +
                "\"ResultStatus\" IS NULL AND " +
                "\"ResultProcessingStatus\" IS NULL AND " +
                "\"ResultRoomId\" IS NOT NULL AND " +
                "\"ResultRoomStatus\" IN (1, 2))");
            table.HasCheckConstraint(
                "CK_properties_property_mutation_operations_kind",
                "\"Kind\" IN (1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint(
                "CK_properties_property_mutation_operations_resource",
                "(\"Kind\" IN (1, 2, 3, 4, 5) AND " +
                "\"ResourceKind\" = 1 AND " +
                "\"ResourceId\" = \"PropertyId\") OR " +
                "(\"Kind\" = 6 AND " +
                "\"ResourceKind\" = 2 AND " +
                "\"ResourceId\" = \"ResultRoomId\")");
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
        builder.Property(operation => operation.RequestFingerprint)
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(operation => operation.Kind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(operation => operation.ResourceKind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(operation => operation.ResourceId).IsRequired();
        builder.Property(operation => operation.ResultStatus)
            .HasConversion<int?>();
        builder.Property(operation => operation.ResultProcessingStatus)
            .HasConversion<int?>();
        builder.Property(operation => operation.ResultRoomStatus)
            .HasConversion<int?>();
        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(operation => new
            {
                operation.ScopeId,
                operation.PropertyId
            })
            .HasPrincipalKey(property => new
            {
                property.ScopeId,
                property.Id
            })
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(operation => new
        {
            operation.ScopeId,
            operation.CompletedAtUtc,
            operation.ResourceKind,
            operation.ResourceId,
            operation.Id
        });
    }
}
