namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class GuestAnonymisationTombstoneConfiguration
    : IEntityTypeConfiguration<GuestAnonymisationTombstone>
{
    public void Configure(EntityTypeBuilder<GuestAnonymisationTombstone> builder)
    {
        builder.ToTable("guest_anonymisation_tombstones", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_anonymisation_tombstones_contract",
                $"\"ContractVersion\" = {GuestAnonymisationTombstone.CurrentContractVersion}");
            table.HasCheckConstraint(
                "CK_guest_anonymisation_tombstones_revision",
                "\"Revision\" >= 1");
            table.HasCheckConstraint(
                "CK_guest_anonymisation_tombstones_state",
                $"\"State\" = {(int)GuestAnonymisationTombstoneState.Anonymised}");
            table.HasCheckConstraint(
                "CK_guest_anonymisation_tombstones_receipt_digest",
                $"char_length(\"OwnerReceiptSha256\") = {GuestAnonymisationReceipt.Sha256Length}");
        });
        builder.HasKey(tombstone => tombstone.Id);
        builder.HasAlternateKey(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.Id
        });
        builder.Property(tombstone => tombstone.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(tombstone => tombstone.Revision)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(tombstone => tombstone.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(tombstone => tombstone.OwnerReceiptSha256)
            .HasMaxLength(GuestAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.CompletedAtUtc,
            tombstone.Id
        });
        builder.HasOne<GuestProfile>()
            .WithOne()
            .HasForeignKey<GuestAnonymisationTombstone>(
                tombstone => new { tombstone.ScopeId, tombstone.Id })
            .HasPrincipalKey<GuestProfile>(
                profile => new { profile.ScopeId, profile.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GuestAnonymisationReceipt>()
            .WithOne()
            .HasForeignKey<GuestAnonymisationTombstone>(
                tombstone => new
                {
                    tombstone.ScopeId,
                    tombstone.OwnerReceiptSha256
                })
            .HasPrincipalKey<GuestAnonymisationReceipt>(
                receipt => new
                {
                    receipt.ScopeId,
                    receipt.CanonicalSha256
                })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(tombstone => tombstone.DomainEvents);
    }
}
