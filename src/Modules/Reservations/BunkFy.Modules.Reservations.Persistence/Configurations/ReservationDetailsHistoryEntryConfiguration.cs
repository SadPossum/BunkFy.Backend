namespace BunkFy.Modules.Reservations.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class ReservationDetailsHistoryEntryConfiguration
    : IEntityTypeConfiguration<ReservationDetailsHistoryEntry>
{
    public void Configure(EntityTypeBuilder<ReservationDetailsHistoryEntry> builder)
    {
        builder.ToTable("reservation_details_history");
        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(entry => entry.Origin).HasConversion<int>().IsRequired();
        builder.Property(entry => entry.ActorId).HasMaxLength(200);
        builder.Property(entry => entry.OperationDeduplicationKey).HasMaxLength(80).IsRequired();
        builder.Property(entry => entry.ChangedFieldsJson).HasMaxLength(4000).IsRequired();
        builder.Property(entry => entry.BeforeSnapshotJson).HasMaxLength(32_768);
        builder.Property(entry => entry.AfterSnapshotJson).HasMaxLength(32_768).IsRequired();
        builder.Property(entry => entry.AfterSnapshotHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(entry => new { entry.ScopeId, entry.ReservationId, entry.ToRevision }).IsUnique();
        builder.HasIndex(entry => new { entry.ScopeId, entry.OperationDeduplicationKey }).IsUnique();
        builder.HasIndex(entry => new { entry.ScopeId, entry.PropertyId, entry.OccurredAtUtc });
    }
}
