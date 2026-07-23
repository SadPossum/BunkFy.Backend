namespace BunkFy.Modules.Guests.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestPropertyProjectionConfiguration : IEntityTypeConfiguration<GuestPropertyProjection>
{
    public void Configure(EntityTypeBuilder<GuestPropertyProjection> builder)
    {
        builder.ToTable("property_projection", table =>
        {
            table.HasCheckConstraint(
                "CK_guests_property_projection_versions",
                "\"TopologySourceVersion\" >= 0 AND \"PolicySourceVersion\" >= 0");
            table.HasCheckConstraint(
                "CK_guests_property_projection_processing_status",
                "\"ProcessingStatus\" BETWEEN 1 AND 3");
            table.HasCheckConstraint(
                "CK_guests_property_projection_governance_policy",
                "(\"ProcessingStatus\" = 1 AND \"OperatingCountryCode\" IS NULL AND " +
                "\"JurisdictionPolicyId\" IS NULL AND \"JurisdictionPolicyVersion\" IS NULL AND " +
                "\"DataRegionId\" IS NULL AND \"TransferProfileId\" IS NULL AND " +
                "\"RetentionPolicyId\" IS NULL AND \"RetentionPolicyVersion\" IS NULL AND " +
                "\"PolicyContentSha256\" IS NULL AND \"PolicyEffectiveAtUtc\" IS NULL AND " +
                "\"PolicyExpiresAtUtc\" IS NULL AND \"PolicyActivatedAtUtc\" IS NULL) OR " +
                "(\"ProcessingStatus\" IN (2, 3) AND \"OperatingCountryCode\" IS NOT NULL AND " +
                "\"JurisdictionPolicyId\" IS NOT NULL AND \"JurisdictionPolicyVersion\" IS NOT NULL AND " +
                "\"DataRegionId\" IS NOT NULL AND \"TransferProfileId\" IS NOT NULL AND " +
                "\"RetentionPolicyId\" IS NOT NULL AND \"RetentionPolicyVersion\" IS NOT NULL AND " +
                "\"PolicyContentSha256\" IS NOT NULL AND \"PolicyEffectiveAtUtc\" IS NOT NULL AND " +
                "\"PolicyExpiresAtUtc\" IS NOT NULL AND \"PolicyActivatedAtUtc\" IS NOT NULL AND " +
                "\"JurisdictionPolicyVersion\" > 0 AND \"RetentionPolicyVersion\" > 0 AND " +
                "char_length(\"OperatingCountryCode\") = 2 AND char_length(\"PolicyContentSha256\") = 64 AND " +
                "\"PolicyEffectiveAtUtc\" < \"PolicyExpiresAtUtc\" AND " +
                "\"PolicyActivatedAtUtc\" >= \"PolicyEffectiveAtUtc\" AND " +
                "\"PolicyActivatedAtUtc\" < \"PolicyExpiresAtUtc\")");
        });
        builder.HasKey(property => new { property.ScopeId, property.Id });
        builder.Property(property => property.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(property => property.Name).HasMaxLength(256);
        builder.Property(property => property.Status).HasConversion<int>().IsRequired();
        builder.Property(property => property.TopologySourceVersion).IsConcurrencyToken().IsRequired();
        builder.Property(property => property.PolicySourceVersion).IsConcurrencyToken().IsRequired();
        builder.Property(property => property.ProcessingStatus)
            .HasConversion<int>()
            .HasDefaultValue(BunkFy.Modules.Properties.Contracts.PropertyProcessingStatus.Unconfigured)
            .IsRequired();
        builder.HasIndex(property => new { property.ScopeId, property.Status, property.Id });
        builder.OwnsOne(property => property.GovernancePolicy, policy =>
        {
            policy.Property(value => value.OperatingCountryCode)
                .HasColumnName("OperatingCountryCode")
                .HasMaxLength(BunkFy.Modules.Properties.Contracts.PropertiesContractLimits.CountryCodeLength);
            policy.Property(value => value.PolicyId)
                .HasColumnName("JurisdictionPolicyId")
                .HasMaxLength(BunkFy.Modules.Properties.Contracts.PropertiesContractLimits.PolicyKeyMaxLength);
            policy.Property(value => value.PolicyVersion).HasColumnName("JurisdictionPolicyVersion");
            policy.Property(value => value.DataRegionId)
                .HasColumnName("DataRegionId")
                .HasMaxLength(BunkFy.Modules.Properties.Contracts.PropertiesContractLimits.PolicyKeyMaxLength);
            policy.Property(value => value.TransferProfileId)
                .HasColumnName("TransferProfileId")
                .HasMaxLength(BunkFy.Modules.Properties.Contracts.PropertiesContractLimits.PolicyKeyMaxLength);
            policy.Property(value => value.RetentionPolicyId)
                .HasColumnName("RetentionPolicyId")
                .HasMaxLength(BunkFy.Modules.Properties.Contracts.PropertiesContractLimits.PolicyKeyMaxLength);
            policy.Property(value => value.RetentionPolicyVersion).HasColumnName("RetentionPolicyVersion");
            policy.Property(value => value.ContentSha256)
                .HasColumnName("PolicyContentSha256")
                .HasMaxLength(BunkFy.Modules.Properties.Contracts.PropertiesContractLimits.ContentSha256Length);
            policy.Property(value => value.PolicyEffectiveAtUtc).HasColumnName("PolicyEffectiveAtUtc");
            policy.Property(value => value.PolicyExpiresAtUtc).HasColumnName("PolicyExpiresAtUtc");
            policy.Property(value => value.ActivatedAtUtc).HasColumnName("PolicyActivatedAtUtc");
            policy.OwnsMany(value => value.Acknowledgements, acknowledgements =>
            {
                acknowledgements.ToTable("property_policy_acknowledgements");
                acknowledgements.WithOwner().HasForeignKey("ScopeId", "PropertyId");
                acknowledgements.Property<string>("ScopeId").HasMaxLength(128);
                acknowledgements.Property<Guid>("PropertyId");
                acknowledgements.HasKey(
                    "ScopeId",
                    "PropertyId",
                    nameof(GuestPropertyPolicyAcknowledgement.AcknowledgementId),
                    nameof(GuestPropertyPolicyAcknowledgement.AcknowledgementVersion));
                acknowledgements.Property(value => value.AcknowledgementId)
                    .HasMaxLength(BunkFy.Modules.Properties.Contracts.PropertiesContractLimits.PolicyKeyMaxLength)
                    .IsRequired();
                acknowledgements.Property(value => value.AcknowledgementVersion).IsRequired();
            });
            policy.Navigation(value => value.Acknowledgements).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }
}
