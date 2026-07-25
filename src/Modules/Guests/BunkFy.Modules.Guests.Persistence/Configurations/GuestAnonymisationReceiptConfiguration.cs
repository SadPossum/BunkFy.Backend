namespace BunkFy.Modules.Guests.Persistence.Configurations;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using BunkFy.Modules.Guests.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainDisposition = BunkFy.Modules.Guests.Domain.Models.GuestAnonymisationDisposition;
using DomainReason = BunkFy.Modules.Guests.Domain.Models.GuestAnonymisationReason;

internal sealed class GuestAnonymisationReceiptConfiguration
    : IEntityTypeConfiguration<GuestAnonymisationReceipt>
{
    public void Configure(EntityTypeBuilder<GuestAnonymisationReceipt> builder)
    {
        builder.ToTable("guest_anonymisation_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_guest_anonymisation_receipts_contract",
                $"\"ContractVersion\" = {GuestAnonymisationReceipt.CurrentContractVersion}");
            table.HasCheckConstraint(
                "CK_guest_anonymisation_receipts_revisions",
                "\"ApprovalRevision\" >= 1 AND \"OperationRevision\" > \"ApprovalRevision\"");
            table.HasCheckConstraint(
                "CK_guest_anonymisation_receipts_versions",
                "\"SelectedGuestVersion\" >= 1 AND " +
                "\"ResultingGuestVersion\" = \"SelectedGuestVersion\" + 1");
            table.HasCheckConstraint(
                "CK_guest_anonymisation_receipts_outcome",
                $"\"Disposition\" = {(int)DomainDisposition.Completed} AND " +
                $"\"Reason\" = {(int)DomainReason.ProfileAnonymised}");
            table.HasCheckConstraint(
                "CK_guest_anonymisation_receipts_property_count",
                $"\"AffectedPropertyCount\" BETWEEN 1 AND " +
                $"{GuestAnonymisationEligibilityContract.MaximumAffectedProperties}");
            table.HasCheckConstraint(
                "CK_guest_anonymisation_receipts_digests",
                $"char_length(\"ApprovalEvidenceSha256\") = {GuestAnonymisationReceipt.Sha256Length} AND " +
                $"char_length(\"PolicySetSha256\") = {GuestAnonymisationReceipt.Sha256Length} AND " +
                $"char_length(\"CanonicalSha256\") = {GuestAnonymisationReceipt.Sha256Length}");
            table.HasCheckConstraint(
                "CK_guest_anonymisation_receipts_actor",
                "length(trim(\"ActorId\")) > 0");
        });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new { receipt.ScopeId, receipt.Id });
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.CanonicalSha256
        });
        builder.Property(receipt => receipt.ScopeId).HasMaxLength(128).IsRequired();
        builder.Property(receipt => receipt.Disposition).HasConversion<int>().IsRequired();
        builder.Property(receipt => receipt.Reason).HasConversion<int>().IsRequired();
        builder.Property(receipt => receipt.ApprovalEvidenceSha256)
            .HasMaxLength(GuestAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.PolicySetSha256)
            .HasMaxLength(GuestAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(GuestAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(GuestProfile.ActorIdMaxLength)
            .IsRequired();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.IdempotencyKey
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.CaseId,
            receipt.ApprovalRevision,
            receipt.OperationRevision,
            receipt.GuestId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.GuestId,
            receipt.ResultingGuestVersion
        }).IsUnique();
        builder.HasOne<GuestProfile>()
            .WithMany()
            .HasForeignKey(receipt => new { receipt.ScopeId, receipt.GuestId })
            .HasPrincipalKey(profile => new { profile.ScopeId, profile.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
