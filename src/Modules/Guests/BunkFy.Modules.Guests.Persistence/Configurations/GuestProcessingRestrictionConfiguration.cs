namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestProcessingRestrictionConfiguration
    : IEntityTypeConfiguration<GuestProcessingRestriction>
{
    public void Configure(EntityTypeBuilder<GuestProcessingRestriction> builder)
    {
        builder.ToTable("guest_processing_restrictions", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_processing_restrictions_apply_approval",
                "\"ApplyApprovalRevision\" >= 1 AND \"ApplySelectedGuestVersion\" >= 1");
            table.HasCheckConstraint(
                "CK_guest_processing_restrictions_lifecycle",
                "(\"Status\" = 1 AND \"ReleaseCaseId\" IS NULL AND " +
                "\"ReleaseApprovalRevision\" IS NULL AND " +
                "\"ReleaseSelectedGuestVersion\" IS NULL AND " +
                "\"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR " +
                "(\"Status\" = 2 AND \"ReleaseCaseId\" IS NOT NULL AND " +
                "\"ReleaseApprovalRevision\" >= 1 AND " +
                "\"ReleaseSelectedGuestVersion\" >= 1 AND " +
                "\"ReleasedBy\" IS NOT NULL AND \"ReleasedAtUtc\" IS NOT NULL AND " +
                "\"ReleasedAtUtc\" >= \"AppliedAtUtc\" AND \"Version\" >= 2)");
        });
        builder.HasKey(restriction => restriction.Id);
        builder.HasAlternateKey(restriction => new { restriction.ScopeId, restriction.Id });
        builder.Property(restriction => restriction.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(restriction => restriction.Status).HasConversion<int>().IsRequired();
        builder.Property(restriction => restriction.Version).IsConcurrencyToken();
        builder.Property(restriction => restriction.AppliedBy)
            .HasMaxLength(GuestProfile.ActorIdMaxLength)
            .IsRequired();
        builder.Property(restriction => restriction.ReleasedBy)
            .HasMaxLength(GuestProfile.ActorIdMaxLength);
        builder.HasIndex(restriction => new
        {
            restriction.ScopeId,
            restriction.PropertyId,
            restriction.GuestId,
            restriction.ApplyCaseId,
            restriction.ApplyApprovalRevision
        }).IsUnique();
        builder.HasIndex(restriction => new
        {
            restriction.ScopeId,
            restriction.PropertyId,
            restriction.GuestId,
            restriction.Status,
            restriction.AppliedAtUtc
        });
        builder.Ignore(restriction => restriction.DomainEvents);
    }
}
