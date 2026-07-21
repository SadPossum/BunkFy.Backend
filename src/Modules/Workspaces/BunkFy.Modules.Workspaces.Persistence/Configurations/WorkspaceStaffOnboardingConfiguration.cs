namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkspaceStaffOnboardingConfiguration
    : IEntityTypeConfiguration<WorkspaceStaffOnboarding>
{
    public void Configure(EntityTypeBuilder<WorkspaceStaffOnboarding> builder)
    {
        builder.ToTable("staff_onboarding_applications", table =>
        {
            table.HasCheckConstraint("CK_staff_onboarding_version", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_staff_onboarding_source", "\"SourceKind\" IN (1, 2)");
            table.HasCheckConstraint("CK_staff_onboarding_status", "\"Status\" BETWEEN 1 AND 8");
            table.HasCheckConstraint("CK_staff_onboarding_claim",
                "(\"ClaimId\" IS NULL AND \"ClaimVersion\" IS NULL) OR " +
                "(\"ClaimId\" IS NOT NULL AND \"ClaimVersion\" > 0)");
            table.HasCheckConstraint("CK_staff_onboarding_staff",
                "\"Status\" NOT IN (4, 5) OR \"StaffMemberId\" IS NOT NULL");
            table.HasCheckConstraint("CK_staff_onboarding_pending_profile",
                "\"Status\" IN (5, 7, 8) OR " +
                "(\"VerifiedAccountEmail\" IS NOT NULL AND \"DisplayName\" IS NOT NULL)");
            table.HasCheckConstraint("CK_staff_onboarding_terminal_redaction",
                "\"Status\" NOT IN (5, 7, 8) OR " +
                "(\"VerifiedAccountEmail\" IS NULL AND \"DisplayName\" IS NULL AND " +
                "\"LegalName\" IS NULL AND \"WorkEmail\" IS NULL AND \"WorkPhone\" IS NULL AND " +
                "\"EmployeeNumber\" IS NULL AND \"JobTitle\" IS NULL AND \"Department\" IS NULL)");
        });
        builder.HasKey(application => application.Id);
        builder.HasAlternateKey(application => new { application.ScopeId, application.Id });
        builder.Property(application => application.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(application => application.SourceKind).HasConversion<int>().IsRequired();
        builder.Property(application => application.SubjectId)
            .HasMaxLength(WorkspaceStaffOnboardingRules.SubjectIdMaxLength).IsRequired();
        builder.Property(application => application.VerifiedAccountEmail)
            .HasMaxLength(WorkspaceStaffOnboardingRules.EmailMaxLength);
        builder.Property(application => application.DisplayName)
            .HasMaxLength(WorkspaceStaffOnboardingRules.DisplayNameMaxLength);
        builder.Property(application => application.LegalName)
            .HasMaxLength(WorkspaceStaffOnboardingRules.LegalNameMaxLength);
        builder.Property(application => application.WorkEmail)
            .HasMaxLength(WorkspaceStaffOnboardingRules.EmailMaxLength);
        builder.Property(application => application.WorkPhone)
            .HasMaxLength(WorkspaceStaffOnboardingRules.PhoneMaxLength);
        builder.Property(application => application.EmployeeNumber)
            .HasMaxLength(WorkspaceStaffOnboardingRules.EmployeeNumberMaxLength);
        builder.Property(application => application.JobTitle)
            .HasMaxLength(WorkspaceStaffOnboardingRules.JobTitleMaxLength);
        builder.Property(application => application.Department)
            .HasMaxLength(WorkspaceStaffOnboardingRules.DepartmentMaxLength);
        builder.Property(application => application.Status).HasConversion<int>().IsRequired();
        builder.Property(application => application.FailureCode)
            .HasMaxLength(WorkspaceStaffOnboardingRules.FailureCodeMaxLength);
        builder.Property(application => application.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(application => new
        {
            application.ScopeId,
            application.SourceKind,
            application.SourceId,
            application.SubjectId
        }).IsUnique();
        builder.HasIndex(application => new
        {
            application.ScopeId,
            application.ClaimId
        }).IsUnique();
        builder.HasIndex(application => new
        {
            application.ScopeId,
            application.Status,
            application.CreatedAtUtc,
            application.Id
        });
        builder.Ignore(application => application.IsAdmissible);
        builder.Ignore(application => application.DomainEvents);
    }
}
