namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Runs;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ObservationReceiptConfiguration : IEntityTypeConfiguration<ObservationReceipt>
{
    public void Configure(EntityTypeBuilder<ObservationReceipt> builder)
    {
        builder.ToTable("observation_receipts");
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(receipt => receipt.SourceRecordType).HasMaxLength(ObservationReceipt.SourceRecordTypeMaxLength).IsRequired();
        builder.Property(receipt => receipt.ExternalId).HasMaxLength(ObservationReceipt.ExternalIdMaxLength).IsRequired();
        builder.Property(receipt => receipt.SourceRevision).HasMaxLength(ObservationReceipt.SourceRevisionMaxLength);
        builder.Property(receipt => receipt.DeduplicationKey).HasMaxLength(ObservationReceipt.DeduplicationKeyMaxLength).IsRequired();
        builder.Property(receipt => receipt.ContentHash).HasMaxLength(ObservationReceipt.ContentHashLength).IsFixedLength().IsRequired();
        builder.Property(receipt => receipt.RawPayloadRetentionState).HasConversion<int>().IsRequired();
        builder.Property(receipt => receipt.RawPayloadVersion).IsConcurrencyToken().IsRequired();
        builder.Property(receipt => receipt.State).HasConversion<int>().IsRequired();
        builder.Property(receipt => receipt.RejectionReason).HasMaxLength(ObservationReceipt.RejectionReasonMaxLength);
        builder.Property(receipt => receipt.ParserType).HasMaxLength(ObservationReceipt.ParserTypeMaxLength);
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.ConnectionId, receipt.OperationId }).IsUnique();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.ConnectionId, receipt.DeduplicationKey }).IsUnique();
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.ConnectionId, receipt.State, receipt.ReceivedAtUtc });
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.RawPayloadRetentionState,
            receipt.RawPayloadRetainUntilUtc,
            receipt.RawPayloadPurgeStartedAtUtc
        });
        builder.HasIndex(receipt => new { receipt.ScopeId, receipt.SourceReceiptId, receipt.ReceivedAtUtc });
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ActiveReprocessingAttemptId,
            receipt.ReprocessingReservationExpiresAtUtc
        });
        builder.HasOne<AdapterConnection>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.ConnectionId })
            .HasPrincipalKey(connection => new { connection.ScopeId, connection.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<IngestionRun>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.RunId })
            .HasPrincipalKey(run => new { run.ScopeId, run.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ObservationReceipt>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.SourceReceiptId })
            .HasPrincipalKey(receipt => new { receipt.ScopeId, receipt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ObservationReprocessingAttempt>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.ReprocessingAttemptId })
            .HasPrincipalKey(attempt => new { attempt.ScopeId, attempt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
