namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffProcessingRestrictionConfiguration
    : IEntityTypeConfiguration<StaffProcessingRestriction>
{
    public void Configure(
        EntityTypeBuilder<StaffProcessingRestriction> builder)
    {
        builder.ToTable("staff_processing_restrictions", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_processing_restrictions_apply_approval",
                "\"ApplyApprovalRevision\" >= 1 AND " +
                "\"ApplySelectedStaffVersion\" >= 1");
            table.HasCheckConstraint(
                "CK_staff_processing_restrictions_lifecycle",
                "(\"Status\" = 1 AND \"ReleaseCaseId\" IS NULL AND " +
                "\"ReleaseApprovalRevision\" IS NULL AND " +
                "\"ReleaseSelectedStaffVersion\" IS NULL AND " +
                "\"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL AND " +
                "\"Version\" = 1) OR " +
                "(\"Status\" = 2 AND \"ReleaseCaseId\" IS NOT NULL AND " +
                "\"ReleaseApprovalRevision\" >= 1 AND " +
                "\"ReleaseSelectedStaffVersion\" >= 1 AND " +
                "\"ReleasedBy\" IS NOT NULL AND " +
                "\"ReleasedAtUtc\" IS NOT NULL AND " +
                "\"ReleasedAtUtc\" >= \"AppliedAtUtc\" AND " +
                "\"Version\" >= 2)");
        });
        builder.HasKey(restriction => restriction.Id);
        builder.HasAlternateKey(
            restriction => new { restriction.ScopeId, restriction.Id });
        builder.Property(restriction => restriction.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(restriction => restriction.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(restriction => restriction.Version)
            .IsConcurrencyToken();
        builder.Property(restriction => restriction.AppliedBy)
            .HasMaxLength(StaffMember.ActorIdMaxLength)
            .IsRequired();
        builder.Property(restriction => restriction.ReleasedBy)
            .HasMaxLength(StaffMember.ActorIdMaxLength);
        builder.HasIndex(restriction => new
        {
            restriction.ScopeId,
            restriction.StaffMemberId,
            restriction.ApplyCaseId,
            restriction.ApplyApprovalRevision
        }).IsUnique();
        builder.HasIndex(restriction => new
        {
            restriction.ScopeId,
            restriction.StaffMemberId,
            restriction.ReleaseCaseId,
            restriction.ReleaseApprovalRevision
        }).IsUnique();
        builder.HasIndex(restriction => new
        {
            restriction.ScopeId,
            restriction.StaffMemberId,
            restriction.Status,
            restriction.AppliedAtUtc
        });
        builder.Ignore(restriction => restriction.DomainEvents);
    }
}
