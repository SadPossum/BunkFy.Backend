namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DataRightsCaseConfiguration : IEntityTypeConfiguration<DataRightsCase>
{
    public void Configure(EntityTypeBuilder<DataRightsCase> builder)
    {
        builder.ToTable("cases", table =>
        {
            table.HasCheckConstraint("CK_data_rights_cases_version", "\"Version\" >= 1");
            table.HasCheckConstraint("CK_data_rights_cases_kind", "\"Kind\" IN (1, 2)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_operations",
                "\"RequestedOperations\" BETWEEN 1 AND 31");
            table.HasCheckConstraint(
                "CK_data_rights_cases_restriction_directive",
                "((\"RequestedOperations\" & 4) = 0 AND \"RestrictionDirective\" = 0) OR " +
                "((\"RequestedOperations\" & 4) = 4 AND \"RestrictionDirective\" BETWEEN 0 AND 2)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_requester",
                "\"RequesterRelationship\" IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_requester_scope",
                "(\"Kind\" = 1 AND \"RequesterRelationship\" IN (1, 2, 3)) OR " +
                "(\"Kind\" = 2 AND \"RequesterRelationship\" IN (3, 4))");
            table.HasCheckConstraint(
                "CK_data_rights_cases_verification",
                "\"VerificationStatus\" IN (1, 2, 3, 4)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_routing",
                "\"RoutingStatus\" IN (1, 2, 3)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_status",
                "\"Status\" BETWEEN 1 AND 11");
            table.HasCheckConstraint(
                "CK_data_rights_cases_decision",
                "\"Decision\" BETWEEN 0 AND 2");
            table.HasCheckConstraint(
                "CK_data_rights_cases_decision_reason",
                "\"DecisionReason\" BETWEEN 0 AND 6");
            table.HasCheckConstraint(
                "CK_data_rights_cases_decision_reason_match",
                "(\"Decision\" = 0 AND \"DecisionReason\" = 0) OR " +
                "(\"Decision\" = 1 AND \"DecisionReason\" = 1) OR " +
                "(\"Decision\" = 2 AND \"DecisionReason\" BETWEEN 2 AND 6)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_decision_details",
                "(\"Decision\" = 0 AND \"DecisionRevision\" IS NULL AND " +
                "\"DecidedBy\" IS NULL AND \"DecidedAtUtc\" IS NULL) OR " +
                "(\"Decision\" IN (1, 2) AND \"DecisionRevision\" IS NOT NULL AND " +
                "\"DecisionRevision\" >= 1 AND \"DecisionRevision\" <= \"Version\" AND " +
                "\"DecidedBy\" IS NOT NULL AND \"DecidedAtUtc\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_decision_attribution",
                "\"DecidedBy\" IS NULL OR (length(trim(\"DecidedBy\")) > 0 AND " +
                "\"DecidedAtUtc\" >= \"CreatedAtUtc\" AND " +
                "\"DecidedAtUtc\" <= \"LastChangedAtUtc\")");
            table.HasCheckConstraint(
                "CK_data_rights_cases_decision_state",
                "(\"Decision\" = 0 AND \"Status\" IN (1, 2, 3, 4, 11)) OR " +
                "(\"Decision\" = 1 AND \"Status\" IN (5, 7, 8, 9, 10)) OR " +
                "(\"Decision\" = 2 AND \"Status\" = 6)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_approval_policy_evidence",
                "(\"Decision\" = 1 AND \"RequestedOperations\" = 16 AND " +
                "\"ApprovalEvidenceSchemaVersion\" = 1 AND " +
                "\"ApprovalEvidencePropertyId\" = \"PropertyId\" AND " +
                "\"ApprovalEvidencePropertyVersion\" > 0 AND " +
                "\"ApprovalEvidenceOperatingCountryCode\" IS NOT NULL AND " +
                "char_length(\"ApprovalEvidenceOperatingCountryCode\") = 2 AND " +
                "\"ApprovalEvidencePolicyId\" IS NOT NULL AND " +
                "\"ApprovalEvidencePolicyVersion\" > 0 AND " +
                "\"ApprovalEvidenceRetentionPolicyId\" IS NOT NULL AND " +
                "\"ApprovalEvidenceRetentionPolicyVersion\" > 0 AND " +
                "\"ApprovalEvidenceContentSha256\" IS NOT NULL AND " +
                "char_length(\"ApprovalEvidenceContentSha256\") = 64 AND " +
                "\"ApprovalEvidencePurposeCode\" = 'data-rights-anonymisation' AND " +
                "\"ApprovalEvidenceSurface\" = 'erasure' AND " +
                "\"ApprovalEvidenceSourceProvenance\" = 'authorized-workspace-operator' AND " +
                "\"ApprovalEvidenceEvaluatedAtUtc\" IS NOT NULL AND " +
                "\"ApprovalEvidenceRequiresDistinctExecutor\" = TRUE) OR " +
                "((\"Decision\" <> 1 OR \"RequestedOperations\" <> 16) AND " +
                "\"ApprovalEvidenceSchemaVersion\" IS NULL AND " +
                "\"ApprovalEvidencePropertyId\" IS NULL AND " +
                "\"ApprovalEvidencePropertyVersion\" IS NULL AND " +
                "\"ApprovalEvidenceOperatingCountryCode\" IS NULL AND " +
                "\"ApprovalEvidencePolicyId\" IS NULL AND " +
                "\"ApprovalEvidencePolicyVersion\" IS NULL AND " +
                "\"ApprovalEvidenceRetentionPolicyId\" IS NULL AND " +
                "\"ApprovalEvidenceRetentionPolicyVersion\" IS NULL AND " +
                "\"ApprovalEvidenceContentSha256\" IS NULL AND " +
                "\"ApprovalEvidencePurposeCode\" IS NULL AND " +
                "\"ApprovalEvidenceSurface\" IS NULL AND " +
                "\"ApprovalEvidenceSourceProvenance\" IS NULL AND " +
                "\"ApprovalEvidenceEvaluatedAtUtc\" IS NULL AND " +
                "\"ApprovalEvidenceRequiresDistinctExecutor\" IS NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_property_scope",
                "(\"Kind\" = 1 AND \"PropertyId\" IS NOT NULL) OR " +
                "(\"Kind\" = 2 AND \"PropertyId\" IS NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_cases_timestamps",
                "\"LastChangedAtUtc\" >= \"CreatedAtUtc\" AND " +
                "(\"DueAtUtc\" IS NULL OR \"DueAtUtc\" >= \"CreatedAtUtc\")");
            table.HasCheckConstraint(
                "CK_data_rights_cases_created_by",
                "length(trim(\"CreatedBy\")) > 0");
            table.HasCheckConstraint(
                "CK_data_rights_cases_last_changed_by",
                "length(trim(\"LastChangedBy\")) > 0");
        });
        builder.HasKey(dataRightsCase => dataRightsCase.Id);
        builder.HasAlternateKey(dataRightsCase => new
        {
            dataRightsCase.ScopeId,
            dataRightsCase.Id
        });
        builder.Property(dataRightsCase => dataRightsCase.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.Kind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.RequestedOperations)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.RestrictionAction)
            .HasColumnName("RestrictionDirective")
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.RequesterRelationship)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.VerificationStatus)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.RoutingStatus)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.Decision)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.DecisionReason)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.DecidedBy)
            .HasMaxLength(DataRightsCase.ActorIdMaxLength);
        builder.Property(dataRightsCase => dataRightsCase.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.CreatedBy)
            .HasMaxLength(DataRightsCase.ActorIdMaxLength)
            .IsRequired();
        builder.Property(dataRightsCase => dataRightsCase.LastChangedBy)
            .HasMaxLength(DataRightsCase.ActorIdMaxLength)
            .IsRequired();
        builder.HasIndex(dataRightsCase => new
        {
            dataRightsCase.ScopeId,
            dataRightsCase.PropertyId,
            dataRightsCase.Status,
            dataRightsCase.CreatedAtUtc,
            dataRightsCase.Id
        });
        builder.OwnsOne(dataRightsCase => dataRightsCase.ApprovalPolicyEvidence, evidence =>
        {
            evidence.Property(value => value.SchemaVersion)
                .HasColumnName("ApprovalEvidenceSchemaVersion");
            evidence.Property(value => value.PropertyId)
                .HasColumnName("ApprovalEvidencePropertyId");
            evidence.Property(value => value.PropertyVersion)
                .HasColumnName("ApprovalEvidencePropertyVersion");
            evidence.Property(value => value.OperatingCountryCode)
                .HasColumnName("ApprovalEvidenceOperatingCountryCode")
                .HasMaxLength(DataRightsApprovalPolicyEvidence.CountryCodeLength);
            evidence.Property(value => value.PolicyId)
                .HasColumnName("ApprovalEvidencePolicyId")
                .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
            evidence.Property(value => value.PolicyVersion)
                .HasColumnName("ApprovalEvidencePolicyVersion");
            evidence.Property(value => value.RetentionPolicyId)
                .HasColumnName("ApprovalEvidenceRetentionPolicyId")
                .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
            evidence.Property(value => value.RetentionPolicyVersion)
                .HasColumnName("ApprovalEvidenceRetentionPolicyVersion");
            evidence.Property(value => value.ContentSha256)
                .HasColumnName("ApprovalEvidenceContentSha256")
                .HasMaxLength(DataRightsApprovalPolicyEvidence.ContentSha256Length);
            evidence.Property(value => value.PurposeCode)
                .HasColumnName("ApprovalEvidencePurposeCode")
                .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
            evidence.Property(value => value.Surface)
                .HasColumnName("ApprovalEvidenceSurface")
                .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
            evidence.Property(value => value.SourceProvenance)
                .HasColumnName("ApprovalEvidenceSourceProvenance")
                .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
            evidence.Property(value => value.EvaluatedAtUtc)
                .HasColumnName("ApprovalEvidenceEvaluatedAtUtc");
            evidence.Property(value => value.RequiresDistinctExecutor)
                .HasColumnName("ApprovalEvidenceRequiresDistinctExecutor");
        });
        builder.OwnsMany(dataRightsCase => dataRightsCase.SelectedSubjects, subjects =>
        {
            subjects.ToTable("selected_subjects", table =>
            {
                table.HasCheckConstraint(
                    "CK_data_rights_selected_subjects_owner",
                    "length(trim(\"OwnerKey\")) > 0");
                table.HasCheckConstraint(
                    "CK_data_rights_selected_subjects_record_type",
                    "length(trim(\"RecordType\")) > 0");
                table.HasCheckConstraint(
                    "CK_data_rights_selected_subjects_record_version",
                    "\"RecordVersion\" >= 1");
                table.HasCheckConstraint(
                    "CK_data_rights_selected_subjects_selected_by",
                    "length(trim(\"SelectedBy\")) > 0");
            });
            subjects.WithOwner().HasForeignKey("CaseId");
            subjects.Property<Guid>("CaseId");
            subjects.HasKey(
                "CaseId",
                nameof(DataRightsSubjectCoordinate.OwnerKey),
                nameof(DataRightsSubjectCoordinate.RecordType),
                nameof(DataRightsSubjectCoordinate.RecordId));
            subjects.Property(subject => subject.OwnerKey)
                .HasMaxLength(DataRightsSubjectCoordinate.OwnerKeyMaxLength)
                .IsRequired();
            subjects.Property(subject => subject.RecordType)
                .HasMaxLength(DataRightsSubjectCoordinate.RecordTypeMaxLength)
                .IsRequired();
            subjects.Property(subject => subject.RecordId).ValueGeneratedNever();
            subjects.Property(subject => subject.RecordVersion).IsRequired();
            subjects.Property(subject => subject.SelectedBy)
                .HasMaxLength(DataRightsCase.ActorIdMaxLength)
                .IsRequired();
            subjects.Property(subject => subject.SelectedAtUtc).IsRequired();
        });
        builder.Navigation(dataRightsCase => dataRightsCase.SelectedSubjects)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(dataRightsCase => dataRightsCase.DomainEvents);
    }
}
