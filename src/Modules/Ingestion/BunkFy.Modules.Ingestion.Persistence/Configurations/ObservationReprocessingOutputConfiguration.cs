namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ObservationReprocessingOutputConfiguration
    : IEntityTypeConfiguration<ObservationReprocessingOutput>
{
    public void Configure(EntityTypeBuilder<ObservationReprocessingOutput> builder)
    {
        builder.ToTable("observation_reprocessing_outputs");
        builder.HasKey(output => output.Id);
        builder.HasAlternateKey(output => new { output.ScopeId, output.Id });
        builder.Property(output => output.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(output => output.RecordType)
            .HasMaxLength(ObservationReprocessingOutput.RecordTypeMaxLength).IsRequired();
        builder.Property(output => output.ExternalId)
            .HasMaxLength(ObservationReprocessingOutput.ExternalIdMaxLength).IsRequired();
        builder.Property(output => output.SourceRevision)
            .HasMaxLength(ObservationReprocessingOutput.SourceRevisionMaxLength);
        builder.Property(output => output.ContentHash)
            .HasMaxLength(ObservationReprocessingOutput.ContentHashLength).IsFixedLength().IsRequired();
        builder.Property(output => output.ErrorCode)
            .HasMaxLength(ObservationReprocessingOutput.ErrorCodeMaxLength);
        builder.Property(output => output.Disposition).HasConversion<int>().IsRequired();
        builder.HasIndex(output => new { output.ScopeId, output.AttemptId, output.OutputIndex }).IsUnique();
        builder.HasOne<ObservationReprocessingAttempt>()
            .WithMany()
            .HasForeignKey(output => new { output.ScopeId, output.AttemptId })
            .HasPrincipalKey(attempt => new { attempt.ScopeId, attempt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ObservationReceipt>()
            .WithMany()
            .HasForeignKey(output => new { output.ScopeId, output.ReceiptId })
            .HasPrincipalKey(receipt => new { receipt.ScopeId, receipt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(output => output.DomainEvents);
    }
}
