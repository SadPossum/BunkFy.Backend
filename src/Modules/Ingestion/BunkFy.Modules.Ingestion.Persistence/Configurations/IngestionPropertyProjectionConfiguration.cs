namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class IngestionPropertyProjectionConfiguration
    : IEntityTypeConfiguration<IngestionPropertyProjection>
{
    public void Configure(EntityTypeBuilder<IngestionPropertyProjection> builder)
    {
        builder.ToTable("property_projection", table => table.HasCheckConstraint(
            "CK_property_projection_retention_fence",
            "\"RetentionFenceVersion\" >= 0"));
        builder.HasKey(property => property.Id);
        builder.HasAlternateKey(property => new { property.ScopeId, property.Id });
        builder.Property(property => property.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(property => property.Name).HasMaxLength(IngestionPropertyProjection.NameMaxLength);
        builder.Property(property => property.Code).HasMaxLength(IngestionPropertyProjection.CodeMaxLength);
        builder.Property(property => property.RetentionFenceVersion).IsConcurrencyToken().IsRequired();
        builder.HasIndex(property => new { property.ScopeId, property.IsActive, property.Code });
    }
}
