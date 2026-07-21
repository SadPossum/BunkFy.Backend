namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkspaceStaffAccessPlanPropertyConfiguration
    : IEntityTypeConfiguration<WorkspaceStaffAccessPlanProperty>
{
    public void Configure(EntityTypeBuilder<WorkspaceStaffAccessPlanProperty> builder)
    {
        builder.ToTable("staff_access_plan_properties");
        builder.HasKey(property => new
        {
            property.ScopeId,
            property.PlanId,
            property.PropertyId
        });
        builder.Property(property => property.ScopeId).HasMaxLength(128).IsRequired();
        builder.HasIndex(property => new
        {
            property.ScopeId,
            property.PropertyId,
            property.PlanId
        });
    }
}
