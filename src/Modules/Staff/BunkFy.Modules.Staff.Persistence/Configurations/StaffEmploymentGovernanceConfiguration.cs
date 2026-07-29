namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffEmploymentGovernanceConfiguration
    : IEntityTypeConfiguration<StaffEmploymentGovernance>
{
    public void Configure(
        EntityTypeBuilder<StaffEmploymentGovernance> builder)
    {
        builder.ToTable("staff_employment_governance", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_employment_governance_contract",
                "\"GovernanceContractVersion\" = 1");
            table.HasCheckConstraint(
                "CK_staff_employment_governance_versions",
                "\"SelectedStaffVersion\" >= 1 AND \"Version\" >= 1");
            table.HasCheckConstraint(
                "CK_staff_employment_governance_policy",
                "char_length(\"OperatingCountryCode\") = 2 AND " +
                "\"PolicyVersion\" >= 1 AND " +
                "\"RetentionPolicyVersion\" >= 1 AND " +
                "char_length(\"PolicyContentSha256\") = 64 AND " +
                "\"PolicyEffectiveAtUtc\" < \"PolicyExpiresAtUtc\" AND " +
                "\"PolicyEvaluatedAtUtc\" >= \"PolicyEffectiveAtUtc\" AND " +
                "\"PolicyEvaluatedAtUtc\" < \"PolicyExpiresAtUtc\" AND " +
                "\"ConfiguredAtUtc\" >= \"PolicyEvaluatedAtUtc\" AND " +
                "\"ConfiguredAtUtc\" < \"PolicyExpiresAtUtc\"");
        });
        builder.HasKey(governance => governance.Id);
        builder.HasAlternateKey(governance => new
        {
            governance.ScopeId,
            governance.Id
        });
        builder.Property(governance => governance.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(
                governance => governance.GovernanceContractVersion)
            .IsRequired();
        builder.Property(governance => governance.SelectedStaffVersion)
            .IsRequired();
        builder.Property(governance => governance.ConfiguredBy)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(governance => governance.ConfiguredAtUtc)
            .IsRequired();
        builder.Property(governance => governance.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Ignore(governance => governance.StaffMemberId);
        builder.OwnsOne(governance => governance.Binding, binding =>
        {
            binding.Property(value => value.OperatingCountryCode)
                .HasColumnName("OperatingCountryCode")
                .HasMaxLength(
                    StaffEmploymentGovernanceBinding.CountryCodeLength)
                .IsRequired();
            binding.Property(value => value.PolicyId)
                .HasColumnName("PolicyId")
                .HasMaxLength(
                    StaffEmploymentGovernanceBinding.KeyMaxLength)
                .IsRequired();
            binding.Property(value => value.PolicyVersion)
                .HasColumnName("PolicyVersion")
                .IsRequired();
            binding.Property(value => value.DataRegionId)
                .HasColumnName("DataRegionId")
                .HasMaxLength(
                    StaffEmploymentGovernanceBinding.KeyMaxLength)
                .IsRequired();
            binding.Property(value => value.TransferProfileId)
                .HasColumnName("TransferProfileId")
                .HasMaxLength(
                    StaffEmploymentGovernanceBinding.KeyMaxLength)
                .IsRequired();
            binding.Property(value => value.RetentionPolicyId)
                .HasColumnName("RetentionPolicyId")
                .HasMaxLength(
                    StaffEmploymentGovernanceBinding.KeyMaxLength)
                .IsRequired();
            binding.Property(value => value.RetentionPolicyVersion)
                .HasColumnName("RetentionPolicyVersion")
                .IsRequired();
            binding.Property(value => value.ContentSha256)
                .HasColumnName("PolicyContentSha256")
                .HasMaxLength(
                    StaffEmploymentGovernanceBinding.ContentSha256Length)
                .IsRequired();
            binding.Property(value => value.PolicyEffectiveAtUtc)
                .HasColumnName("PolicyEffectiveAtUtc")
                .IsRequired();
            binding.Property(value => value.PolicyExpiresAtUtc)
                .HasColumnName("PolicyExpiresAtUtc")
                .IsRequired();
            binding.Property(value => value.EvaluatedAtUtc)
                .HasColumnName("PolicyEvaluatedAtUtc")
                .IsRequired();
        });
        builder.OwnsMany(
            governance => governance.AcceptedAcknowledgements,
            acknowledgements =>
            {
                acknowledgements.ToTable(
                    "staff_employment_governance_acknowledgements");
                acknowledgements.WithOwner().HasForeignKey(
                    "ScopeId",
                    "StaffMemberId").HasPrincipalKey(
                    nameof(StaffEmploymentGovernance.ScopeId),
                    nameof(StaffEmploymentGovernance.Id));
                acknowledgements.HasKey(
                    "ScopeId",
                    "StaffMemberId",
                    nameof(
                        StaffEmploymentGovernanceAcknowledgement
                            .AcknowledgementId),
                    nameof(
                        StaffEmploymentGovernanceAcknowledgement
                            .AcknowledgementVersion));
                acknowledgements.Property<string>("ScopeId")
                    .HasMaxLength(128)
                    .IsRequired();
                acknowledgements.Property<Guid>("StaffMemberId")
                    .IsRequired();
                acknowledgements.Property(
                        acknowledgement =>
                            acknowledgement.AcknowledgementId)
                    .HasMaxLength(
                        StaffEmploymentGovernanceBinding.KeyMaxLength)
                    .IsRequired();
                acknowledgements.Property(
                        acknowledgement =>
                            acknowledgement.AcknowledgementVersion)
                    .IsRequired();
                acknowledgements.HasIndex(
                    "ScopeId",
                    "StaffMemberId");
            });
        builder.Navigation(
                governance => governance.AcceptedAcknowledgements)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<StaffMember>()
            .WithOne()
            .HasForeignKey<StaffEmploymentGovernance>(governance => new
            {
                governance.ScopeId,
                governance.Id
            })
            .HasPrincipalKey<StaffMember>(member => new
            {
                member.ScopeId,
                member.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
