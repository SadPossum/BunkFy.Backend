namespace BunkFy.Modules.Staff.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using BunkFy.Modules.Staff.Domain.Entities;

internal sealed class StaffPropertyAssignmentConfiguration : IEntityTypeConfiguration<StaffPropertyAssignment>
{
    public void Configure(EntityTypeBuilder<StaffPropertyAssignment> builder)
    {
        builder.ToTable("property_assignments", table =>
        {
            table.HasCheckConstraint("CK_staff_assignments_versions",
                "\"AssignedAtVersion\" >= 2 AND (\"UnassignedAtVersion\" IS NULL OR \"UnassignedAtVersion\" >= \"AssignedAtVersion\")");
            table.HasCheckConstraint("CK_staff_assignments_lifecycle",
                "(\"IsCurrent\" AND \"EffectiveTo\" IS NULL AND \"UnassignedBy\" IS NULL AND \"UnassignedAtUtc\" IS NULL AND \"UnassignedAtVersion\" IS NULL) OR " +
                "(NOT \"IsCurrent\" AND NOT \"IsPrimary\" AND \"EffectiveTo\" IS NOT NULL AND \"UnassignedBy\" IS NOT NULL AND \"UnassignedAtUtc\" IS NOT NULL AND \"UnassignedAtVersion\" IS NOT NULL)");
            table.HasCheckConstraint("CK_staff_assignments_dates",
                "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" >= \"EffectiveFrom\"");
        });
        builder.HasKey(assignment => new { assignment.ScopeId, assignment.StaffMemberId, assignment.Id });
        builder.Property(assignment => assignment.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(assignment => assignment.PropertyJobTitle).HasMaxLength(StaffPropertyAssignment.JobTitleMaxLength);
        builder.Property(assignment => assignment.AssignedBy).HasMaxLength(StaffPropertyAssignment.ActorIdMaxLength).IsRequired();
        builder.Property(assignment => assignment.UnassignedBy).HasMaxLength(StaffPropertyAssignment.ActorIdMaxLength);
        builder.Property(assignment => assignment.UnassignmentReason).HasMaxLength(StaffPropertyAssignment.ReasonMaxLength);
        builder.HasIndex(assignment => new
        {
            assignment.ScopeId,
            assignment.StaffMemberId,
            assignment.PropertyId,
            assignment.IsCurrent
        });
        builder.HasIndex(assignment => new
        {
            assignment.ScopeId,
            assignment.PropertyId,
            assignment.IsCurrent,
            assignment.StaffMemberId
        });
    }
}
