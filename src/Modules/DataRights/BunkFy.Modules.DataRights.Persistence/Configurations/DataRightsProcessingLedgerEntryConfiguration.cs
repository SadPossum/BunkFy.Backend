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
                $"\"ContractVersion\" = {DataRightsProcessingLedgerEntry.CurrentContractVersion}");
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
                $"\"PolicyEvidenceSchemaVersion\" = {DataRightsApprovalPolicyEvidence.CurrentSchemaVersion} AND " +
                "length(trim(\"PolicyId\")) > 0 AND \"PolicyVersion\" >= 1 AND " +
                "length(trim(\"RetentionPolicyId\")) > 0 AND " +
                "\"RetentionPolicyVersion\" >= 1 AND " +
                $"char_length(\"PolicyContentSha256\") = {DataRightsProcessingLedgerEntry.Sha256Length}");
            table.HasCheckConstraint(
                "CK_data_rights_processing_ledger_receipt",
                "\"OwnerReceiptContractVersion\" >= 1 AND " +
                $"char_length(\"OwnerReceiptSha256\") = {DataRightsProcessingLedgerEntry.Sha256Length}");
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
