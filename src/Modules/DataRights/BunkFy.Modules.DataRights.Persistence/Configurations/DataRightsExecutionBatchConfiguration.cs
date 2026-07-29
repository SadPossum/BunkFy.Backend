namespace BunkFy.Modules.DataRights.Persistence.Configurations;

using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class DataRightsExecutionBatchConfiguration
    : IEntityTypeConfiguration<DataRightsExecutionBatch>
{
    public void Configure(EntityTypeBuilder<DataRightsExecutionBatch> builder)
    {
        builder.ToTable("execution_batches", table =>
        {
            table.HasCheckConstraint(
                "CK_data_rights_execution_batches_scope",
                "(\"CaseKind\" = 1 AND \"ScopeKind\" = 1 AND " +
                "\"PropertyId\" IS NOT NULL) OR " +
                "(\"CaseKind\" IN (2, 3) AND \"ScopeKind\" = 2 AND " +
                "\"PropertyId\" IS NULL)");
            table.HasCheckConstraint(
                "CK_data_rights_execution_batches_revisions",
                "\"ApprovalRevision\" >= 1 AND \"ExecutionRevision\" > \"ApprovalRevision\"");
            table.HasCheckConstraint(
                "CK_data_rights_execution_batches_subject_count",
                $"\"SelectedSubjectCount\" BETWEEN 1 AND {DataRightsCase.MaxSelectedSubjects}");
            table.HasCheckConstraint(
                "CK_data_rights_execution_batches_created_by",
                "length(trim(\"CreatedBy\")) > 0");
            table.HasCheckConstraint(
                "CK_data_rights_execution_batches_version",
                "\"Version\" >= 1");
        });

        builder.HasKey(batch => batch.Id);
        builder.HasAlternateKey(batch => new { batch.ScopeId, batch.Id });
        builder.Property(batch => batch.Id).ValueGeneratedNever();
        builder.Property(batch => batch.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(batch => batch.CaseKind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(batch => batch.ScopeKind)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(batch => batch.CreatedBy)
            .HasMaxLength(DataRightsCase.ActorIdMaxLength)
            .IsRequired();
        builder.Property(batch => batch.Version)
            .IsConcurrencyToken()
            .IsRequired();
        builder.HasIndex(batch => new
        {
            batch.ScopeId,
            batch.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(batch => new
        {
            batch.ScopeId,
            batch.CaseId
        }).IsUnique();
        builder.HasOne<DataRightsCase>()
            .WithMany()
            .HasForeignKey(batch => new { batch.ScopeId, batch.CaseId })
            .HasPrincipalKey(dataRightsCase => new { dataRightsCase.ScopeId, dataRightsCase.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(batch => batch.DomainEvents);
    }
}
