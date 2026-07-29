namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffProcessingRestrictionProjectionConfiguration
    : IEntityTypeConfiguration<StaffProcessingRestrictionProjection>
{
    public void Configure(
        EntityTypeBuilder<StaffProcessingRestrictionProjection> builder)
    {
        builder.ToTable("staff_processing_restriction_state", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_processing_restrictions_contract_version",
                "\"ContractVersion\" >= 1");
            table.HasCheckConstraint(
                "CK_staff_processing_restrictions_revision",
                "\"Revision\" >= 0");
            table.HasCheckConstraint(
                "CK_staff_processing_restrictions_effective_state",
                "(\"ActiveRestrictionCount\" = 0 AND " +
                "NOT \"IsRestricted\") OR " +
                "(\"ActiveRestrictionCount\" > 0 AND " +
                "\"IsRestricted\")");
        });
        builder.HasKey(projection => new
        {
            projection.ScopeId,
            projection.StaffMemberId
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
            projection.StaffMemberId
        });
    }
}
