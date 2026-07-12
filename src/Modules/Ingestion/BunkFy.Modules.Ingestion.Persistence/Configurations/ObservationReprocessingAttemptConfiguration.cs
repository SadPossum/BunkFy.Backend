namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ObservationReprocessingAttemptConfiguration
    : IEntityTypeConfiguration<ObservationReprocessingAttempt>
{
    public void Configure(EntityTypeBuilder<ObservationReprocessingAttempt> builder)
    {
        builder.ToTable("observation_reprocessing_attempts");
        builder.HasKey(attempt => attempt.Id);
        builder.HasAlternateKey(attempt => new { attempt.ScopeId, attempt.Id });
        builder.Property(attempt => attempt.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(attempt => attempt.ParserType)
            .HasMaxLength(ObservationReprocessingAttempt.ParserTypeMaxLength).IsRequired();
        builder.Property(attempt => attempt.RequestedBy)
            .HasMaxLength(ObservationReprocessingAttempt.RequestedByMaxLength).IsRequired();
        builder.Property(attempt => attempt.LastErrorCode)
            .HasMaxLength(ObservationReprocessingAttempt.ErrorCodeMaxLength);
        builder.Property(attempt => attempt.State).HasConversion<int>().IsRequired();
        builder.Property(attempt => attempt.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(attempt => new { attempt.ScopeId, attempt.TaskRunId }).IsUnique();
        builder.HasIndex(attempt => new
        {
            attempt.ScopeId,
            attempt.SourceReceiptId,
            attempt.State,
            attempt.RequestedAtUtc
        });
        builder.HasOne<AdapterConnection>()
            .WithMany()
            .HasForeignKey(attempt => new { attempt.ScopeId, attempt.ConnectionId })
            .HasPrincipalKey(connection => new { connection.ScopeId, connection.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ObservationReceipt>()
            .WithMany()
            .HasForeignKey(attempt => new { attempt.ScopeId, attempt.SourceReceiptId })
            .HasPrincipalKey(receipt => new { receipt.ScopeId, receipt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(attempt => attempt.DomainEvents);
    }
}
