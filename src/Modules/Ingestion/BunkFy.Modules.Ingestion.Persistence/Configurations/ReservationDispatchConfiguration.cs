namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationDispatchConfiguration : IEntityTypeConfiguration<ReservationDispatch>
{
    public void Configure(EntityTypeBuilder<ReservationDispatch> builder)
    {
        builder.ToTable("reservation_dispatches");
        builder.HasKey(dispatch => dispatch.Id);
        builder.HasAlternateKey(dispatch => new { dispatch.ScopeId, dispatch.Id });
        builder.Property(dispatch => dispatch.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(dispatch => dispatch.TriggerKind).HasConversion<int>().IsRequired();
        builder.Property(dispatch => dispatch.Kind).HasConversion<int>().IsRequired();
        builder.Property(dispatch => dispatch.SourceRevision).HasMaxLength(ReservationDispatch.SourceRevisionMaxLength);
        builder.Property(dispatch => dispatch.NormalizedSnapshot).HasMaxLength(ReservationDispatch.NormalizedSnapshotMaxLength);
        builder.Property(dispatch => dispatch.State).HasConversion<int>().IsRequired();
        builder.Property(dispatch => dispatch.ErrorCode).HasMaxLength(ReservationDispatch.ErrorCodeMaxLength);
        builder.Property(dispatch => dispatch.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(dispatch => new { dispatch.ScopeId, dispatch.TriggerKind, dispatch.TriggerId }).IsUnique();
        builder.HasIndex(dispatch => new { dispatch.ScopeId, dispatch.ReceiptId, dispatch.CreatedAtUtc });
        builder.HasIndex(dispatch => new { dispatch.ScopeId, dispatch.SourceLinkId, dispatch.CreatedAtUtc });
        builder.HasIndex(dispatch => new { dispatch.ScopeId, dispatch.ReservationId, dispatch.Kind, dispatch.State });
        builder.HasIndex(dispatch => new
        {
            dispatch.ScopeId,
            dispatch.ConnectionId,
            dispatch.SensitiveDataRetainUntilUtc,
            dispatch.SensitiveDataRedactedAtUtc
        });
        builder.HasOne<ReservationSourceLink>()
            .WithMany()
            .HasForeignKey(dispatch => new { dispatch.ScopeId, dispatch.SourceLinkId })
            .HasPrincipalKey(link => new { link.ScopeId, link.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ObservationReceipt>()
            .WithMany()
            .HasForeignKey(dispatch => new { dispatch.ScopeId, dispatch.ReceiptId })
            .HasPrincipalKey(receipt => new { receipt.ScopeId, receipt.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(dispatch => dispatch.DomainEvents);
    }
}
