namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationSourceLinkConfiguration : IEntityTypeConfiguration<ReservationSourceLink>
{
    public void Configure(EntityTypeBuilder<ReservationSourceLink> builder)
    {
        builder.ToTable("reservation_source_links");
        builder.HasKey(link => link.Id);
        builder.HasAlternateKey(link => new { link.ScopeId, link.Id });
        builder.Property(link => link.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(link => link.SourceSystem).HasMaxLength(ReservationSourceLink.SourceSystemMaxLength).IsRequired();
        builder.Property(link => link.SourceReference).HasMaxLength(ReservationSourceLink.SourceReferenceMaxLength).IsRequired();
        builder.Property(link => link.LastObservedSourceRevision).HasMaxLength(ReservationSourceLink.SourceRevisionMaxLength);
        builder.Property(link => link.LastObservedContentHash).HasMaxLength(ReservationSourceLink.ContentHashLength).IsFixedLength().IsRequired();
        builder.Property(link => link.LastAppliedSourceRevision).HasMaxLength(ReservationSourceLink.SourceRevisionMaxLength);
        builder.Property(link => link.LastAppliedOperationalBaseline)
            .HasMaxLength(ReservationSourceLink.OperationalBaselineMaxLength);
        builder.Property(link => link.State).HasConversion<int>().IsRequired();
        builder.Property(link => link.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(link => new { link.ScopeId, link.ConnectionId, link.SourceSystem, link.SourceReference }).IsUnique();
        builder.HasIndex(link => new { link.ScopeId, link.ReservationId });
        builder.HasIndex(link => new { link.ScopeId, link.ConnectionId, link.State, link.UpdatedAtUtc });
        builder.HasOne<AdapterConnection>()
            .WithMany()
            .HasForeignKey(link => new { link.ScopeId, link.ConnectionId })
            .HasPrincipalKey(connection => new { connection.ScopeId, connection.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(link => link.DomainEvents);
    }
}
