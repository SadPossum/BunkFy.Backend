namespace BunkFy.Modules.Retention.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class RetentionPropertyProjectionConfiguration
    : IEntityTypeConfiguration<RetentionPropertyProjection>
{
    public void Configure(EntityTypeBuilder<RetentionPropertyProjection> builder)
    {
        builder.ToTable("property_projection", table =>
        {
            table.HasCheckConstraint(
                "CK_retention_property_projection_versions",
                "\"TopologySourceVersion\" >= 0 AND \"PolicySourceVersion\" >= 0");
            table.HasCheckConstraint(
                "CK_retention_property_projection_policy",
                "(\"PolicySourceVersion\" = 0 AND \"RetentionPolicyVersion\" IS NULL AND " +
                "\"IsProcessingEnabled\" = FALSE) OR " +
                "(\"PolicySourceVersion\" >= 1 AND \"RetentionPolicyVersion\" >= 1)");
        });
        builder.HasKey(property => new { property.ScopeId, property.Id });
        builder.Property(property => property.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(property => property.TopologySourceVersion)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(property => property.PolicySourceVersion)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(property => new
        {
            property.ScopeId,
            property.IsActive,
            property.IsProcessingEnabled,
            property.Id
        });
        builder.Ignore(property => property.IsSchedulable);
    }
}
