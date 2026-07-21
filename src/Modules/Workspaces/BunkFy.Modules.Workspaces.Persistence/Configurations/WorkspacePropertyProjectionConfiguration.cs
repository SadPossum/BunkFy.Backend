namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkspacePropertyProjectionConfiguration
    : IEntityTypeConfiguration<WorkspacePropertyProjection>
{
    public void Configure(EntityTypeBuilder<WorkspacePropertyProjection> builder)
    {
        builder.ToTable("property_projection", table => table.HasCheckConstraint(
            "CK_workspaces_property_projection_version",
            "\"Version\" >= 1"));
        builder.HasKey(property => new { property.ScopeId, property.Id });
        builder.Property(property => property.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(property => property.Name).HasMaxLength(256);
        builder.Property(property => property.Status).HasConversion<int>().IsRequired();
        builder.Property(property => property.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(property => new { property.ScopeId, property.Status, property.Id });
    }
}
