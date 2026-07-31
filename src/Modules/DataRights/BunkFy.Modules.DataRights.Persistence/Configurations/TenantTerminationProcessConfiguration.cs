namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class TenantTerminationProcessConfiguration
    : IEntityTypeConfiguration<TenantTerminationProcess>
{
    public void Configure(EntityTypeBuilder<TenantTerminationProcess> builder)
    {
        builder.ToTable("tenant_termination_processes", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_phase",
                "\"Phase\" BETWEEN 1 AND 6");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_status",
                "\"Status\" BETWEEN 1 AND 6");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_completion",
                "(\"Phase\" = 5 AND \"Status\" = 5) OR " +
                "(\"Phase\" = 6 AND \"Status\" IN (1, 2, 3, 4, 6)) OR " +
                "(\"Phase\" BETWEEN 1 AND 4 AND \"Status\" BETWEEN 1 AND 4)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_approval",
                "\"ApprovalRevision\" >= 1 AND " +
                "char_length(\"PolicyEvidenceSha256\") = 64 AND " +
                "\"PolicyEvidenceSha256\" ~ '^[0-9a-f]{64}$' AND " +
                "length(trim(\"ApprovedBy\")) > 0 AND " +
                "\"ApprovedAtUtc\" <= \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_operation",
                "\"OperationRevision\" >= 0 AND " +
                "((\"Status\" = 1 AND \"OperationRevision\" >= 0) OR " +
                "(\"Status\" BETWEEN 2 AND 6 AND \"OperationRevision\" >= 1))");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_outcome",
                "(\"Status\" = 3 AND \"OutcomeCode\" IS NOT NULL AND " +
                "\"HoldReviewAtUtc\" IS NOT NULL AND " +
                "\"HoldReviewAtUtc\" >= \"LastChangedAtUtc\") OR " +
                "(\"Status\" = 4 AND \"OutcomeCode\" IS NOT NULL AND " +
                "\"HoldReviewAtUtc\" IS NULL) OR " +
                "(\"Status\" IN (1, 2, 5, 6) AND \"OutcomeCode\" IS NULL AND " +
                "\"HoldReviewAtUtc\" IS NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_outcome_code",
                "\"OutcomeCode\" IS NULL OR " +
                "\"OutcomeCode\" ~ '^[a-z0-9][a-z0-9._-]{0,199}$'");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_attribution",
                "length(trim(\"CreatedBy\")) > 0 AND " +
                "length(trim(\"LastChangedBy\")) > 0");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_timestamps",
                "\"LastChangedAtUtc\" >= \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_data_rights_tenant_termination_process_version",
                "\"Version\" >= 1");
        });

        builder.HasKey(process => process.Id);
        builder.HasAlternateKey(process => new { process.ScopeId, process.Id });
        builder.HasAlternateKey(process => new
        {
            process.ScopeId,
            process.Id,
            process.CaseId,
            process.ApprovalRevision,
            process.TerminationEpoch,
            process.PolicyEvidenceSha256
        });
        builder.Property(process => process.Id).ValueGeneratedNever();
        builder.Property(process => process.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(process => process.PolicyEvidenceSha256)
            .HasMaxLength(TenantTerminationProcess.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(process => process.ApprovedBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength)
            .IsRequired();
        builder.Property(process => process.Phase)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(process => process.Status)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(process => process.OutcomeCode)
            .HasMaxLength(TenantTerminationProcess.OutcomeCodeMaxLength);
        builder.Property(process => process.CreatedBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength)
            .IsRequired();
        builder.Property(process => process.LastChangedBy)
            .HasMaxLength(TenantTerminationProcess.ActorIdMaxLength)
            .IsRequired();
        builder.Property(process => process.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.CaseId
        }).IsUnique();
        builder.HasIndex(process => process.ScopeId)
            .IsUnique()
            .HasFilter("\"Status\" IN (1, 2, 3, 4)");
        builder.HasIndex(process => new
        {
            process.ScopeId,
            process.Status,
            process.Phase,
            process.LastChangedAtUtc,
            process.Id
        });
        builder.HasOne<DataRightsCase>()
            .WithOne()
            .HasForeignKey<TenantTerminationProcess>(process => new
            {
                process.ScopeId,
                process.CaseId
            })
            .HasPrincipalKey<DataRightsCase>(dataRightsCase => new
            {
                dataRightsCase.ScopeId,
                dataRightsCase.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(process => process.DomainEvents);
    }
}
