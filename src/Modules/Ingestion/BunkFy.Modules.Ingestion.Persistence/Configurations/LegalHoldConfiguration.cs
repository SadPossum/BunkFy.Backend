namespace BunkFy.Modules.Ingestion.Persistence.Configurations;

using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class LegalHoldConfiguration : IEntityTypeConfiguration<LegalHold>
{
    public void Configure(EntityTypeBuilder<LegalHold> builder)
    {
        builder.ToTable("legal_holds", table =>
        {
            table.HasCheckConstraint(
                "CK_legal_holds_reason",
                "length(trim(\"Reason\")) > 0");
            table.HasCheckConstraint(
                "CK_legal_holds_placed_by",
                "length(trim(\"PlacedBy\")) > 0");
            table.HasCheckConstraint(
                "CK_legal_holds_version",
                "\"Version\" >= 1");
            table.HasCheckConstraint(
                "CK_legal_holds_lifecycle",
                "(\"State\" = 1 AND \"ReleasedBy\" IS NULL AND \"ReleaseReason\" IS NULL AND \"ReleasedAtUtc\" IS NULL) OR " +
                "(\"State\" = 2 AND \"ReleasedBy\" IS NOT NULL AND length(trim(\"ReleasedBy\")) > 0 AND " +
                "\"ReleaseReason\" IS NOT NULL AND length(trim(\"ReleaseReason\")) > 0 AND " +
                "\"ReleasedAtUtc\" IS NOT NULL AND \"ReleasedAtUtc\" >= \"PlacedAtUtc\")");
        });
        builder.HasKey(legalHold => legalHold.Id);
        builder.HasAlternateKey(legalHold => new { legalHold.ScopeId, legalHold.Id });
        builder.Property(legalHold => legalHold.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(legalHold => legalHold.Reason)
            .HasMaxLength(LegalHold.ReasonMaxLength).IsRequired();
        builder.Property(legalHold => legalHold.State).HasConversion<int>().IsRequired();
        builder.Property(legalHold => legalHold.PlacedBy)
            .HasMaxLength(LegalHold.ActorMaxLength).IsRequired();
        builder.Property(legalHold => legalHold.ReleasedBy)
            .HasMaxLength(LegalHold.ActorMaxLength);
        builder.Property(legalHold => legalHold.ReleaseReason)
            .HasMaxLength(LegalHold.ReasonMaxLength);
        builder.Property(legalHold => legalHold.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(legalHold => new
        {
            legalHold.ScopeId,
            legalHold.PropertyId,
            legalHold.State
        });
        builder.HasIndex(legalHold => new
        {
            legalHold.ScopeId,
            legalHold.PropertyId,
            legalHold.PlacedAtUtc,
            legalHold.Id
        });
        builder.HasOne<IngestionPropertyProjection>()
            .WithMany()
            .HasForeignKey(legalHold => new { legalHold.ScopeId, legalHold.PropertyId })
            .HasPrincipalKey(property => new { property.ScopeId, property.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(legalHold => legalHold.DomainEvents);
    }
}
