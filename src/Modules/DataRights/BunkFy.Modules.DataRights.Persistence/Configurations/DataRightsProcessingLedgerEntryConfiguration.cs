namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DataRightsProcessingLedgerEntryConfiguration
    : IEntityTypeConfiguration<DataRightsProcessingLedgerEntry>
{
    public void Configure(EntityTypeBuilder<DataRightsProcessingLedgerEntry> builder)
    {
        builder.ToTable("processing_ledger_entries", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_contract",
                $"\"ContractVersion\" BETWEEN " +
                $"{DataRightsProcessingLedgerEntry.MinimumSupportedContractVersion} AND " +
                $"{DataRightsProcessingLedgerEntry.CurrentContractVersion}");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_sequence",
                "\"TenantSequence\" >= 1");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_revisions",
                "\"ApprovalRevision\" >= 1 AND \"OperationRevision\" > \"ApprovalRevision\"");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_operation",
                "\"Operation\" = 16");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_scope",
                "((\"ContractVersion\" IN (1, 2) AND \"CaseKind\" = 0 AND " +
                "\"ScopeKind\" = 0 AND \"RoutingPropertyId\" IS NOT NULL) OR " +
                "(\"ContractVersion\" = 3 AND " +
                "((\"CaseKind\" = 1 AND \"ScopeKind\" = 1 AND " +
                "\"RoutingPropertyId\" IS NOT NULL) OR " +
                "(\"CaseKind\" IN (2, 3) AND \"ScopeKind\" = 2 AND " +
                "\"RoutingPropertyId\" IS NULL))))");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_subject",
                "length(trim(\"OwnerKey\")) > 0 AND " +
                "length(trim(\"RecordType\")) > 0 AND " +
                "\"RecordPseudonymKeyVersion\" >= 1 AND " +
                $"char_length(\"RecordPseudonymSha256\") = {DataRightsProcessingLedgerEntry.Sha256Length}");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_outcome",
                "length(trim(\"DispositionCode\")) > 0 AND " +
                "length(trim(\"ReasonCode\")) > 0 AND " +
                "\"CompletedAtUtc\" <> '-infinity'");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_policy",
                "\"PolicyEvidenceSchemaVersion\" IN (1, 2) AND " +
                "length(trim(\"PolicyId\")) > 0 AND \"PolicyVersion\" >= 1 AND " +
                "length(trim(\"RetentionPolicyId\")) > 0 AND " +
                "\"RetentionPolicyVersion\" >= 1 AND " +
                $"char_length(\"PolicyContentSha256\") = {DataRightsProcessingLedgerEntry.Sha256Length} AND " +
                "((\"ContractVersion\" IN (1, 2) AND " +
                "\"PolicyEvidenceSchemaVersion\" = 1 AND " +
                "\"PolicyPropertyVersion\" IS NULL AND " +
                "\"PolicyOperatingCountryCode\" IS NULL AND " +
                "\"PolicyPurposeCode\" IS NULL AND " +
                "\"PolicySurface\" IS NULL AND " +
                "\"PolicySourceProvenance\" IS NULL AND " +
                "\"PolicyRetentionDataClass\" IS NULL AND " +
                "\"PolicyRetentionTrigger\" IS NULL AND " +
                "\"PolicyRetentionTriggeredAtUtc\" IS NULL AND " +
                "\"PolicyRetentionDeadlineUtc\" IS NULL AND " +
                "\"PolicyEvaluatedAtUtc\" IS NULL AND " +
                "\"PolicyStateBindingsJson\" IS NULL AND " +
                "\"PolicyStateBindingsSha256\" IS NULL AND " +
                "\"PolicyRequiresDistinctExecutor\" IS NULL) OR " +
                "(\"ContractVersion\" = 3 AND " +
                "\"PolicyEvidenceSchemaVersion\" = 2 AND " +
                "((\"ScopeKind\" = 1 AND \"PolicyPropertyVersion\" >= 1) OR " +
                "(\"ScopeKind\" = 2 AND \"PolicyPropertyVersion\" = 0)) AND " +
                $"char_length(\"PolicyOperatingCountryCode\") = {DataRightsApprovalPolicyEvidence.CountryCodeLength} AND " +
                "length(trim(\"PolicyPurposeCode\")) > 0 AND " +
                "length(trim(\"PolicySurface\")) > 0 AND " +
                "length(trim(\"PolicySourceProvenance\")) > 0 AND " +
                "length(trim(\"PolicyRetentionDataClass\")) > 0 AND " +
                "length(trim(\"PolicyRetentionTrigger\")) > 0 AND " +
                "\"PolicyRetentionTriggeredAtUtc\" IS NOT NULL AND " +
                "\"PolicyRetentionDeadlineUtc\" > " +
                    "\"PolicyRetentionTriggeredAtUtc\" AND " +
                "\"PolicyRetentionDeadlineUtc\" <= " +
                    "\"PolicyEvaluatedAtUtc\" AND " +
                "length(\"PolicyStateBindingsJson\") > 0 AND " +
                $"length(\"PolicyStateBindingsJson\") <= {DataRightsApprovalPolicyEvidence.StateBindingsJsonMaxLength} AND " +
                $"char_length(\"PolicyStateBindingsSha256\") = {DataRightsProcessingLedgerEntry.Sha256Length} AND " +
                "\"PolicyRequiresDistinctExecutor\"))");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_receipt",
                "\"OwnerReceiptContractVersion\" >= 1 AND " +
                $"char_length(\"OwnerReceiptSha256\") = {DataRightsProcessingLedgerEntry.Sha256Length}");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_result_version",
                $"(\"ContractVersion\" = {DataRightsProcessingLedgerEntry.MinimumSupportedContractVersion} AND " +
                "\"ResultingRecordVersion\" IS NULL) OR " +
                $"(\"ContractVersion\" >= {DataRightsProcessingLedgerEntry.GuestResultVersionContractVersion} AND " +
                "\"ResultingRecordVersion\" >= 1)");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_chain",
                $"char_length(\"PreviousEntrySha256\") = {DataRightsProcessingLedgerEntry.Sha256Length} AND " +
                $"char_length(\"EntrySha256\") = {DataRightsProcessingLedgerEntry.Sha256Length} AND " +
                $"((\"TenantSequence\" = 1 AND \"PreviousEntrySha256\" = '{DataRightsProcessingLedgerEntry.GenesisEntrySha256}') OR " +
                $"(\"TenantSequence\" > 1 AND \"PreviousEntrySha256\" <> '{DataRightsProcessingLedgerEntry.GenesisEntrySha256}'))");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_replay",
                "(\"ReplayOfLedgerEntryId\" IS NULL OR \"ReplayOfLedgerEntryId\" <> \"Id\") AND " +
                "(\"SupersedesLedgerEntryId\" IS NULL OR \"SupersedesLedgerEntryId\" <> \"Id\") AND " +
                "(\"ReplayOfLedgerEntryId\" IS NULL OR \"SupersedesLedgerEntryId\" IS NULL OR " +
                "\"ReplayOfLedgerEntryId\" <> \"SupersedesLedgerEntryId\")");
        });

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.Id).ValueGeneratedNever();
        builder.Property(entry => entry.OwnerKey)
            .HasMaxLength(DataRightsSubjectCoordinate.OwnerKeyMaxLength)
            .IsRequired();
        builder.Property(entry => entry.RecordType)
            .HasMaxLength(DataRightsSubjectCoordinate.RecordTypeMaxLength)
            .IsRequired();
        builder.Property(entry => entry.RecordPseudonymSha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(entry => entry.DispositionCode)
            .HasMaxLength(DataRightsProcessingLedgerEntry.CodeMaxLength)
            .IsRequired();
        builder.Property(entry => entry.ReasonCode)
            .HasMaxLength(DataRightsProcessingLedgerEntry.CodeMaxLength)
            .IsRequired();
        builder.Property(entry => entry.PolicyId)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength)
            .IsRequired();
        builder.Property(entry => entry.PolicyContentSha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(entry => entry.RetentionPolicyId)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength)
            .IsRequired();
        builder.Property(entry => entry.OwnerReceiptSha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(entry => entry.PreviousEntrySha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(entry => entry.EntrySha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(entry => entry.Operation)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(entry => entry.CaseKind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(entry => entry.ScopeKind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(entry => entry.PolicyOperatingCountryCode)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.CountryCodeLength)
            .IsFixedLength();
        builder.Property(entry => entry.PolicyPurposeCode)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
        builder.Property(entry => entry.PolicySurface)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
        builder.Property(entry => entry.PolicySourceProvenance)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
        builder.Property(entry => entry.PolicyRetentionDataClass)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
        builder.Property(entry => entry.PolicyRetentionTrigger)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength);
        builder.Property(entry => entry.PolicyStateBindingsJson)
            .HasMaxLength(
                DataRightsApprovalPolicyEvidence.StateBindingsJsonMaxLength);
        builder.Property(entry => entry.PolicyStateBindingsSha256)
            .HasMaxLength(DataRightsProcessingLedgerEntry.Sha256Length)
            .IsFixedLength();

        builder.HasIndex(entry => new
        {
            entry.ScopeId,
            entry.TenantSequence
        }).IsUnique();
        builder.HasIndex(entry => new
        {
            entry.ScopeId,
            entry.WorkItemId
        }).IsUnique();
        builder.HasIndex(entry => new
        {
            entry.ScopeId,
            entry.OwnerReceiptId
        }).IsUnique();
        builder.HasIndex(entry => new
        {
            entry.ScopeId,
            entry.CaseId,
            entry.OperationRevision,
            entry.OwnerKey,
            entry.RecordType,
            entry.RecordPseudonymSha256
        }).IsUnique();
        builder.HasIndex(entry => new
        {
            entry.ScopeId,
            entry.EntrySha256
        }).IsUnique();
        builder.HasIndex(entry => new
        {
            entry.ScopeId,
            entry.RoutingPropertyId,
            entry.CompletedAtUtc,
            entry.Id
        });

    }
}
