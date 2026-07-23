namespace BunkFy.Modules.Properties.Persistence.Configurations;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class PropertyGovernanceRevisionConfiguration
    : IEntityTypeConfiguration<PropertyGovernanceRevision>
{
    public void Configure(EntityTypeBuilder<PropertyGovernanceRevision> builder)
    {
        builder.ToTable("property_governance_revisions", table =>
        {
            table.HasCheckConstraint("CK_property_governance_revision_version", "\"PropertyVersion\" >= 2");
            table.HasCheckConstraint("CK_property_governance_revision_action", "\"Action\" BETWEEN 1 AND 4");
        });
        builder.HasKey(revision => revision.Id);
        builder.Property(revision => revision.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(revision => revision.Action).HasConversion<int>().IsRequired();
        builder.Property(revision => revision.DecisionReasonCode).HasMaxLength(64).IsRequired();
        builder.Property(revision => revision.ActorId).HasMaxLength(Property.ActorIdMaxLength).IsRequired();
        builder.HasIndex(revision => new
        {
            revision.ScopeId,
            revision.PropertyId,
            revision.PropertyVersion
        }).IsUnique();
        builder.HasIndex(revision => new
        {
            revision.ScopeId,
            revision.PropertyId,
            revision.OccurredAtUtc
        });
        builder.HasOne<Property>()
            .WithMany()
            .HasForeignKey(revision => new { revision.ScopeId, revision.PropertyId })
            .HasPrincipalKey(property => new { property.ScopeId, property.Id })
            .OnDelete(DeleteBehavior.Restrict);

        ConfigureCoordinates(builder.OwnsOne(revision => revision.Previous), "Previous");
        ConfigureCoordinates(builder.OwnsOne(revision => revision.Current), "Current");
    }

    private static void ConfigureCoordinates(
        OwnedNavigationBuilder<PropertyGovernanceRevision, PropertyGovernanceRevisionCoordinatesRecord> coordinates,
        string prefix)
    {
        coordinates.Property(value => value.OperatingCountryCode)
            .HasColumnName($"{prefix}OperatingCountryCode")
            .HasMaxLength(Property.CountryCodeLength);
        coordinates.Property(value => value.PolicyId)
            .HasColumnName($"{prefix}JurisdictionPolicyId")
            .HasMaxLength(Property.PolicyKeyMaxLength);
        coordinates.Property(value => value.PolicyVersion)
            .HasColumnName($"{prefix}JurisdictionPolicyVersion");
        coordinates.Property(value => value.DataRegionId)
            .HasColumnName($"{prefix}DataRegionId")
            .HasMaxLength(Property.PolicyKeyMaxLength);
        coordinates.Property(value => value.TransferProfileId)
            .HasColumnName($"{prefix}TransferProfileId")
            .HasMaxLength(Property.PolicyKeyMaxLength);
        coordinates.Property(value => value.RetentionPolicyId)
            .HasColumnName($"{prefix}RetentionPolicyId")
            .HasMaxLength(Property.PolicyKeyMaxLength);
        coordinates.Property(value => value.RetentionPolicyVersion)
            .HasColumnName($"{prefix}RetentionPolicyVersion");
        coordinates.Property(value => value.ContentSha256)
            .HasColumnName($"{prefix}PolicyContentSha256")
            .HasMaxLength(Property.ContentSha256Length);
        coordinates.Property(value => value.AcknowledgementSetSha256)
            .HasColumnName($"{prefix}AcknowledgementSetSha256")
            .HasMaxLength(Property.ContentSha256Length);
    }
}
