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
                "\"ResultVersion\" >= \"ExpectedVersion\" AND " +
                "\"ResultVersion\" <= \"ExpectedVersion\" + 1");
            table.HasCheckConstraint(
                "CK_properties_property_mutation_operations_fingerprint",
                "char_length(\"RequestFingerprint\") = 64 AND " +
                "\"RequestFingerprint\" ~ '^[0-9a-f]{64}$'");
            table.HasCheckConstraint(
                "CK_properties_property_mutation_operations_status",
                "\"ResultStatus\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_properties_property_mutation_operations_processing",
                "\"ResultProcessingStatus\" IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_properties_property_mutation_operations_kind",
                "\"Kind\" IN (1, 2, 3, 4)");
        });
        builder.HasKey(operation => new
        {
            operation.ScopeId,
            operation.PropertyId,
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
        builder.Property(operation => operation.ResultStatus)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(operation => operation.ResultProcessingStatus)
            .HasConversion<int>()
            .IsRequired();
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
            operation.Id
        });
    }
}
