namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    WorkspaceStaffOnboardingProcessingRestrictionProjectionConfiguration
    : IEntityTypeConfiguration<
        WorkspaceStaffOnboardingProcessingRestrictionProjection>
{
    public void Configure(
        EntityTypeBuilder<
            WorkspaceStaffOnboardingProcessingRestrictionProjection> builder)
    {
        builder.ToTable(
            "staff_onboarding_processing_restriction_state",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ws_onboarding_restriction_contract",
                    "\"ContractVersion\" >= 1");
                table.HasCheckConstraint(
                    "CK_ws_onboarding_restriction_revision",
                    "\"Revision\" >= 0");
                table.HasCheckConstraint(
                    "CK_ws_onboarding_restriction_state",
                    "(\"ActiveRestrictionCount\" = 0 AND " +
                    "NOT \"IsRestricted\") OR " +
                    "(\"ActiveRestrictionCount\" > 0 AND " +
                    "\"IsRestricted\")");
            });
        builder.HasKey(projection => new
        {
            projection.ScopeId,
            projection.ApplicationId
        });
        builder.Property(projection => projection.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(projection => projection.ProjectionOrdinal)
            .ValueGeneratedOnAdd()
            .IsRequired();
        builder.Property(projection => projection.Revision)
            .IsConcurrencyToken();
        builder.HasIndex(projection => projection.ProjectionOrdinal)
            .IsUnique();
        builder.HasIndex(projection => new
        {
            projection.ScopeId,
            projection.IsRestricted,
            projection.ApplicationId
        });
    }
}
