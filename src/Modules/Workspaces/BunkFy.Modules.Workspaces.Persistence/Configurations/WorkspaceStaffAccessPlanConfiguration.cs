namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkspaceStaffAccessPlanConfiguration
    : IEntityTypeConfiguration<WorkspaceStaffAccessPlan>
{
    public void Configure(EntityTypeBuilder<WorkspaceStaffAccessPlan> builder)
    {
        builder.ToTable("staff_access_plans", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_access_plans_source",
                "\"SourceKind\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_staff_access_plans_status",
                "\"Status\" IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_staff_access_plans_version",
                "\"Version\" >= 1");
        });
        builder.HasKey(plan => plan.Id);
        builder.HasAlternateKey(plan => new { plan.ScopeId, plan.Id });
        builder.Property(plan => plan.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(plan => plan.SourceKind).HasConversion<int>().IsRequired();
        builder.Property(plan => plan.ProfileKey)
            .HasMaxLength(WorkspaceStaffAccessPlan.ProfileKeyMaxLength).IsRequired();
        builder.Property(plan => plan.CreatedBySubjectId)
            .HasMaxLength(WorkspaceStaffAccessPlan.SubjectIdMaxLength).IsRequired();
        builder.Property(plan => plan.Status).HasConversion<int>().IsRequired();
        builder.Property(plan => plan.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(plan => new
        {
            plan.ScopeId,
            plan.Status,
            plan.CreatedAtUtc,
            plan.Id
        });
        builder.HasMany(plan => plan.Properties)
            .WithOne()
            .HasForeignKey(property => new { property.ScopeId, property.PlanId })
            .HasPrincipalKey(plan => new { plan.ScopeId, plan.Id })
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(plan => plan.Properties)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(plan => plan.DomainEvents);
    }
}
