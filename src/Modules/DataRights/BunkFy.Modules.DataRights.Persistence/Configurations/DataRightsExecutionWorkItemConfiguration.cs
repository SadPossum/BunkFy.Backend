namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DataRightsExecutionWorkItemConfiguration
    : IEntityTypeConfiguration<DataRightsExecutionWorkItem>
{
    public void Configure(EntityTypeBuilder<DataRightsExecutionWorkItem> builder)
    {
        builder.ToTable("execution_work_items", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_revisions",
                "\"ApprovalRevision\" >= 1 AND \"ExecutionRevision\" > \"ApprovalRevision\"");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_operation",
                "\"Operation\" = 16");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_subject",
                "length(trim(\"OwnerKey\")) > 0 AND " +
                "length(trim(\"RecordType\")) > 0 AND \"SelectedRecordVersion\" >= 1");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_policy",
                "\"PolicyEvidenceSchemaVersion\" = 1 AND " +
                "length(trim(\"PolicyId\")) > 0 AND \"PolicyVersion\" >= 1 AND " +
                "length(trim(\"RetentionPolicyId\")) > 0 AND " +
                "\"RetentionPolicyVersion\" >= 1 AND " +
                $"char_length(\"PolicyContentSha256\") = {DataRightsApprovalPolicyEvidence.ContentSha256Length}");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_state",
                "\"State\" BETWEEN 1 AND 7");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_attempts",
                "\"AttemptCount\" >= 0");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_owner_contract",
                $"\"OwnerContractVersion\" = {DataRightsExecutionWorkItem.CurrentOwnerContractVersion}");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_task",
                "(\"State\" = 1 AND \"TaskRunId\" IS NULL AND " +
                "\"LastTaskAttempt\" = 0 AND \"LastAttemptAtUtc\" IS NULL AND " +
                "\"AttemptCount\" = 0) OR " +
                "(\"State\" BETWEEN 2 AND 7 AND \"TaskRunId\" IS NOT NULL AND " +
                "\"LastTaskAttempt\" >= 1 AND \"LastAttemptAtUtc\" IS NOT NULL AND " +
                "\"AttemptCount\" >= 1)");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_owner_outcome",
                "((\"State\" IN (5, 7)) AND " +
                "\"OwnerReceiptContractVersion\" >= 1 AND " +
                "\"OwnerReceiptId\" IS NOT NULL AND " +
                "\"ResultingRecordVersion\" > \"SelectedRecordVersion\" AND " +
                "length(trim(\"OwnerDispositionCode\")) > 0 AND " +
                "length(trim(\"OwnerReasonCode\")) > 0 AND " +
                $"char_length(\"OwnerReceiptSha256\") = {DataRightsExecutionWorkItem.Sha256Length} AND " +
                "\"OwnerCompletedAtUtc\" IS NOT NULL AND " +
                "\"OutcomeCode\" IS NULL AND \"OutcomeAtUtc\" IS NOT NULL AND " +
                "\"OwnerCompletedAtUtc\" <= \"OutcomeAtUtc\") OR " +
                "((\"State\" IN (3, 4, 6)) AND " +
                "\"OwnerReceiptContractVersion\" IS NULL AND " +
                "\"OwnerReceiptId\" IS NULL AND \"ResultingRecordVersion\" IS NULL AND " +
                "\"OwnerDispositionCode\" IS NULL AND \"OwnerReasonCode\" IS NULL AND " +
                "\"OwnerReceiptSha256\" IS NULL AND \"OwnerCompletedAtUtc\" IS NULL AND " +
                "length(trim(\"OutcomeCode\")) > 0 AND \"OutcomeAtUtc\" IS NOT NULL) OR " +
                "((\"State\" IN (1, 2)) AND " +
                "\"OwnerReceiptContractVersion\" IS NULL AND " +
                "\"OwnerReceiptId\" IS NULL AND \"ResultingRecordVersion\" IS NULL AND " +
                "\"OwnerDispositionCode\" IS NULL AND \"OwnerReasonCode\" IS NULL AND " +
                "\"OwnerReceiptSha256\" IS NULL AND \"OwnerCompletedAtUtc\" IS NULL AND " +
                "\"OutcomeCode\" IS NULL AND \"OutcomeAtUtc\" IS NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_timestamps",
                "(\"LastAttemptAtUtc\" IS NULL OR \"LastAttemptAtUtc\" >= \"CreatedAtUtc\") AND " +
                "(\"OwnerCompletedAtUtc\" IS NULL OR \"OwnerCompletedAtUtc\" >= \"CreatedAtUtc\") AND " +
                "(\"OutcomeAtUtc\" IS NULL OR \"OutcomeAtUtc\" >= \"CreatedAtUtc\")");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_created_by",
                "length(trim(\"CreatedBy\")) > 0");
            table.HasCheckConstraint(
                "CK_data_rights_execution_work_items_version",
                "\"Version\" >= 1");
        });

        builder.HasKey(workItem => workItem.Id);
        builder.Property(workItem => workItem.Id).ValueGeneratedNever();
        builder.Property(workItem => workItem.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(workItem => workItem.Operation)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(workItem => workItem.OwnerKey)
            .HasMaxLength(DataRightsSubjectCoordinate.OwnerKeyMaxLength)
            .IsRequired();
        builder.Property(workItem => workItem.RecordType)
            .HasMaxLength(DataRightsSubjectCoordinate.RecordTypeMaxLength)
            .IsRequired();
        builder.Property(workItem => workItem.PolicyId)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength)
            .IsRequired();
        builder.Property(workItem => workItem.RetentionPolicyId)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.KeyMaxLength)
            .IsRequired();
        builder.Property(workItem => workItem.PolicyContentSha256)
            .HasMaxLength(DataRightsApprovalPolicyEvidence.ContentSha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(workItem => workItem.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(workItem => workItem.OwnerDispositionCode)
            .HasMaxLength(DataRightsExecutionWorkItem.OwnerCodeMaxLength);
        builder.Property(workItem => workItem.OwnerReasonCode)
            .HasMaxLength(DataRightsExecutionWorkItem.OwnerCodeMaxLength);
        builder.Property(workItem => workItem.OwnerReceiptSha256)
            .HasMaxLength(DataRightsExecutionWorkItem.Sha256Length)
            .IsFixedLength();
        builder.Property(workItem => workItem.OutcomeCode)
            .HasMaxLength(DataRightsExecutionWorkItem.OutcomeCodeMaxLength);
        builder.Property(workItem => workItem.CreatedBy)
            .HasMaxLength(DataRightsCase.ActorIdMaxLength)
            .IsRequired();
        builder.Property(workItem => workItem.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasIndex(workItem => new
        {
            workItem.ScopeId,
            workItem.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(workItem => new
        {
            workItem.ScopeId,
            workItem.CaseId,
            workItem.ApprovalRevision,
            workItem.Operation,
            workItem.OwnerKey,
            workItem.RecordType,
            workItem.RecordId
        }).IsUnique();
        builder.HasIndex(workItem => new
        {
            workItem.ScopeId,
            workItem.PropertyId,
            workItem.State,
            workItem.CreatedAtUtc,
            workItem.Id
        });
        builder.HasIndex(workItem => new
        {
            workItem.ScopeId,
            workItem.TaskRunId
        }).IsUnique();
        builder.HasOne<DataRightsCase>()
            .WithMany()
            .HasForeignKey(workItem => new { workItem.ScopeId, workItem.CaseId })
            .HasPrincipalKey(dataRightsCase => new { dataRightsCase.ScopeId, dataRightsCase.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(workItem => workItem.DomainEvents);
    }
}
