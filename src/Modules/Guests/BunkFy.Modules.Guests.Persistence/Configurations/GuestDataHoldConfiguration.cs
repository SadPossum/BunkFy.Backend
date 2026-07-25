namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestDataHoldConfiguration : IEntityTypeConfiguration<GuestDataHold>
{
    public void Configure(EntityTypeBuilder<GuestDataHold> builder)
    {
        builder.ToTable("data_holds", table =>
        {
            table.HasCheckConstraint("CK_guest_data_holds_version", "\"Version\" >= 1");
            table.HasCheckConstraint(
                "CK_guest_data_holds_lifecycle",
                "(\"State\" = 1 AND \"ReleasedBy\" IS NULL AND \"ReleasedAtUtc\" IS NULL AND \"Version\" = 1) OR " +
                "(\"State\" = 2 AND \"ReleasedBy\" IS NOT NULL AND \"ReleasedAtUtc\" IS NOT NULL AND " +
                "\"ReleasedAtUtc\" >= \"PlacedAtUtc\" AND \"Version\" >= 2)");
        });
        builder.HasKey(hold => hold.Id);
        builder.HasAlternateKey(hold => new { hold.ScopeId, hold.Id });
        builder.Property(hold => hold.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(hold => hold.ReasonCode)
            .HasMaxLength(GuestDataHold.ReasonCodeMaxLength)
            .IsRequired();
        builder.Property(hold => hold.State).HasConversion<int>().IsRequired();
        builder.Property(hold => hold.PlacedBy)
            .HasMaxLength(GuestProfile.ActorIdMaxLength)
            .IsRequired();
        builder.Property(hold => hold.ReleasedBy)
            .HasMaxLength(GuestProfile.ActorIdMaxLength);
        builder.Property(hold => hold.Version).IsConcurrencyToken().IsRequired();
        builder.HasIndex(hold => new
        {
            hold.ScopeId,
            hold.GuestId,
            hold.State,
            hold.PropertyId,
            hold.Id
        });
        builder.HasIndex(hold => new
        {
            hold.ScopeId,
            hold.PropertyId,
            hold.GuestId,
            hold.PlacedAtUtc,
            hold.Id
        });
        builder.HasIndex(hold => new
        {
            hold.ScopeId,
            hold.PropertyId,
            hold.GuestId,
            hold.State,
            hold.PlacedAtUtc,
            hold.Id
        });
    }
}
