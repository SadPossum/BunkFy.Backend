namespace BunkFy.Modules.Staff.Persistence.Configurations;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class StaffAnonymisationTombstoneConfiguration
    : IEntityTypeConfiguration<StaffAnonymisationTombstone>
{
    public void Configure(
        EntityTypeBuilder<StaffAnonymisationTombstone> builder)
    {
        builder.ToTable("staff_anonymisation_tombstones", table =>
        {
            table.HasCheckConstraint(
                "CK_staff_anonymisation_tombstones_contract",
                $"\"ContractVersion\" = " +
                $"{StaffAnonymisationTombstone.CurrentContractVersion}");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_tombstones_revision",
                "\"Revision\" >= 1");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_tombstones_state",
                $"\"State\" = " +
                $"{(int)StaffAnonymisationTombstoneState.Anonymised}");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_tombstones_authority",
                $"\"Authority\" = " +
                $"{(int)StaffAnonymisationAuthority.DataRights}");
            table.HasCheckConstraint(
                "CK_staff_anonymisation_tombstones_receipt_digest",
                $"char_length(\"OwnerReceiptSha256\") = " +
                $"{StaffAnonymisationReceipt.Sha256Length}");
        });
        builder.HasKey(tombstone => tombstone.Id);
        builder.HasAlternateKey(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.Id
        });
        builder.Property(tombstone => tombstone.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(tombstone => tombstone.Revision)
            .IsConcurrencyToken()
            .IsRequired();
        builder.Property(tombstone => tombstone.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(tombstone => tombstone.Authority)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(tombstone => tombstone.OwnerReceiptSha256)
            .HasMaxLength(StaffAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.CompletedAtUtc,
            tombstone.Id
        });
        builder.HasOne<StaffMember>()
            .WithOne()
            .HasForeignKey<StaffAnonymisationTombstone>(
                tombstone => new
                {
                    tombstone.ScopeId,
                    tombstone.Id
                })
            .HasPrincipalKey<StaffMember>(
                member => new
                {
                    member.ScopeId,
                    member.Id
                })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(tombstone => tombstone.DomainEvents);
    }
}
