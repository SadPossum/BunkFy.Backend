namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DataRightsCorrectionExecutionConfiguration
    : IEntityTypeConfiguration<DataRightsCorrectionExecution>
{
    public void Configure(EntityTypeBuilder<DataRightsCorrectionExecution> builder)
    {
        builder.ToTable("correction_executions", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_correction_executions_contract",
                $"\"ContractVersion\" = {DataRightsCorrectionExecution.CurrentContractVersion}");
            table.HasCheckConstraint(
                "CK_data_rights_correction_executions_revisions",
                "\"SelectedCaseVersion\" >= 1 AND " +
                "\"ExecutionRevision\" = \"SelectedCaseVersion\" + 1 AND " +
                "\"ApprovalRevision\" >= 1 AND " +
                "\"SelectedRecordVersion\" >= 1 AND \"Version\" >= 1");
            table.HasCheckConstraint(
                "CK_data_rights_correction_executions_coordinates",
                $"((\"CaseKind\" = {(int)DataRightsCaseKind.GuestRights} AND " +
                "\"PropertyId\" IS NOT NULL) OR " +
                $"(\"CaseKind\" IN ({(int)DataRightsCaseKind.TenantTermination}, " +
                $"{(int)DataRightsCaseKind.StaffRights}) AND \"PropertyId\" IS NULL)) AND " +
                "\"CaseId\" IS NOT NULL AND " +
                "\"RecordId\" IS NOT NULL AND length(trim(\"OwnerKey\")) > 0 AND " +
                "length(trim(\"RecordType\")) > 0 AND " +
                "length(trim(\"FieldPolicyKey\")) > 0 AND " +
                "length(trim(\"ExecutedBy\")) > 0");
            table.HasCheckConstraint(
                "CK_data_rights_correction_executions_timestamps",
                "\"StartedAtUtc\" IS NOT NULL AND \"ExpiresAtUtc\" > \"StartedAtUtc\" AND " +
                "\"ExpiresAtUtc\" <= \"StartedAtUtc\" + INTERVAL '15 minutes'");
            table.HasCheckConstraint(
                "CK_data_rights_correction_executions_state",
                $"(\"State\" = {(int)DataRightsCorrectionExecutionState.Claimed} AND " +
                "\"ReceiptContractVersion\" IS NULL AND \"ReceiptId\" IS NULL AND " +
                "\"CurrentRecordVersion\" IS NULL AND \"ChangedFieldCount\" IS NULL AND " +
                "\"ChangedFieldsSha256\" IS NULL AND \"ReceiptSha256\" IS NULL AND " +
                "\"CompletedAtUtc\" IS NULL) OR " +
                $"(\"State\" = {(int)DataRightsCorrectionExecutionState.Completed} AND " +
                "\"ReceiptContractVersion\" >= 1 AND \"ReceiptId\" IS NOT NULL AND " +
                "\"CurrentRecordVersion\" = \"SelectedRecordVersion\" + 1 AND " +
                "\"ChangedFieldCount\" BETWEEN 1 AND 32 AND " +
                "char_length(\"ChangedFieldsSha256\") = 64 AND " +
                "char_length(\"ReceiptSha256\") = 64 AND " +
                "\"CompletedAtUtc\" BETWEEN \"StartedAtUtc\" AND \"ExpiresAtUtc\")");
        });
        builder.HasKey(execution => execution.Id);
        builder.HasAlternateKey(execution => new { execution.ScopeId, execution.Id });
        builder.Property(execution => execution.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(execution => execution.ContractVersion).IsRequired();
        builder.Property(execution => execution.CaseKind).HasConversion<int>().IsRequired();
        builder.Property(execution => execution.OwnerKey)
            .HasMaxLength(DataRightsSubjectCoordinate.OwnerKeyMaxLength)
            .IsRequired();
        builder.Property(execution => execution.RecordType)
            .HasMaxLength(DataRightsSubjectCoordinate.RecordTypeMaxLength)
            .IsRequired();
        builder.Property(execution => execution.FieldPolicyKey)
            .HasMaxLength(DataRightsCorrectionExecution.FieldPolicyKeyMaxLength)
            .IsRequired();
        builder.Property(execution => execution.ExecutedBy)
            .HasMaxLength(DataRightsCase.ActorIdMaxLength)
            .IsRequired();
        builder.Property(execution => execution.State).HasConversion<int>().IsRequired();
        builder.Property(execution => execution.ChangedFieldsSha256)
            .HasMaxLength(DataRightsCorrectionExecution.DigestLength)
            .IsFixedLength();
        builder.Property(execution => execution.ReceiptSha256)
            .HasMaxLength(DataRightsCorrectionExecution.DigestLength)
            .IsFixedLength();
        builder.Property(execution => execution.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(execution => new
        {
            execution.ScopeId,
            execution.CaseId
        }).IsUnique();
        builder.HasIndex(execution => new
        {
            execution.ScopeId,
            execution.CaseKind,
            execution.PropertyId,
            execution.State,
            execution.ExpiresAtUtc
        });
        builder.HasOne<DataRightsCase>()
            .WithOne()
            .HasForeignKey<DataRightsCorrectionExecution>(execution => new
            {
                execution.ScopeId,
                execution.CaseId
            })
            .HasPrincipalKey<DataRightsCase>(dataRightsCase => new
            {
                dataRightsCase.ScopeId,
                dataRightsCase.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(execution => execution.DomainEvents);
    }
}
