namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    WorkspaceStaffOnboardingProcessingRestrictionConfiguration
    : IEntityTypeConfiguration<
        WorkspaceStaffOnboardingProcessingRestriction>
{
    public void Configure(
        EntityTypeBuilder<
            WorkspaceStaffOnboardingProcessingRestriction> builder)
    {
        builder.ToTable(
            "staff_onboarding_processing_restrictions",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ws_onboarding_restrictions_apply",
                    "\"ApplyApprovalRevision\" >= 1 AND " +
                    "\"ApplySelectedOnboardingVersion\" >= 1");
                table.HasCheckConstraint(
                    "CK_ws_onboarding_restrictions_lifecycle",
                    "(\"Status\" = 1 AND " +
                    "\"ReleaseCaseId\" IS NULL AND " +
                    "\"ReleaseApprovalRevision\" IS NULL AND " +
                    "\"ReleaseSelectedOnboardingVersion\" IS NULL AND " +
                    "\"ReleasedBy\" IS NULL AND " +
                    "\"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR " +
                    "(\"Status\" = 2 AND " +
                    "\"ReleaseCaseId\" IS NOT NULL AND " +
                    "\"ReleaseApprovalRevision\" >= 1 AND " +
                    "\"ReleaseSelectedOnboardingVersion\" >= 1 AND " +
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
            .HasMaxLength(WorkspaceStaffOnboardingRules.ActorIdMaxLength)
            .IsRequired();
        builder.Property(restriction => restriction.ReleasedBy)
            .HasMaxLength(WorkspaceStaffOnboardingRules.ActorIdMaxLength);
        builder.HasIndex(restriction => new
        {
            restriction.ScopeId,
            restriction.ApplicationId,
            restriction.ApplyCaseId,
            restriction.ApplyApprovalRevision
        }).IsUnique();
        builder.HasIndex(restriction => new
        {
            restriction.ScopeId,
            restriction.ApplicationId,
            restriction.ReleaseCaseId,
            restriction.ReleaseApprovalRevision
        }).IsUnique();
        builder.HasIndex(restriction => new
        {
            restriction.ScopeId,
            restriction.ApplicationId,
            restriction.Status,
            restriction.AppliedAtUtc
        });
        builder.Ignore(restriction => restriction.DomainEvents);
    }
}
