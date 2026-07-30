namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    WorkspaceStaffCorrelationAnonymisationTombstoneConfiguration
    : IEntityTypeConfiguration<
        WorkspaceStaffCorrelationAnonymisationTombstone>
{
    public void Configure(
        EntityTypeBuilder<
            WorkspaceStaffCorrelationAnonymisationTombstone> builder)
    {
        builder.ToTable(
            "staff_correlation_anonymisation_tombstones",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_tombstone_contract",
                    $"\"ContractVersion\" = " +
                    $"{WorkspaceStaffCorrelationAnonymisationTombstone.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_tombstone_revision",
                    "\"Revision\" > 0");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_tombstone_versions",
                    "\"SelectedStaffVersion\" > 0 AND " +
                    "\"SelectedAnchorVersion\" > 0 AND " +
                    "\"ResultingAnchorVersion\" = " +
                    "\"SelectedAnchorVersion\" + 1");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_tombstone_receipt",
                    "\"OwnerReceiptContractVersion\" > 0");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_tombstone_hashes",
                    $"char_length(\"OwnerReceiptSha256\") = " +
                    $"{WorkspaceStaffCorrelationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"ResultingStateSha256\") = " +
                    $"{WorkspaceStaffCorrelationAnonymisationReceipt.Sha256Length}");
                table.HasCheckConstraint(
                    "CK_staff_correlation_anonymisation_tombstone_replay",
                    "(\"LedgerEntryId\" IS NULL AND " +
                    "\"LastReplayedAtUtc\" IS NULL) OR " +
                    "(\"LedgerEntryId\" IS NOT NULL AND " +
                    "\"LastReplayedAtUtc\" IS NOT NULL AND " +
                    "\"LastReplayedAtUtc\" >= \"CompletedAtUtc\")");
            });
        builder.HasKey(tombstone => tombstone.Id);
        builder.Property(tombstone => tombstone.Id)
            .ValueGeneratedNever();
        builder.HasAlternateKey(
            tombstone => new
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
        ConfigureSha(
            builder.Property(
                tombstone => tombstone.OwnerReceiptSha256));
        ConfigureSha(
            builder.Property(
                tombstone => tombstone.ResultingStateSha256));
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.StaffMemberId,
            tombstone.SelectedStaffVersion
        }).IsUnique();
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.OwnerReceiptId
        }).IsUnique();
        builder.HasIndex(tombstone => new
        {
            tombstone.ScopeId,
            tombstone.LedgerEntryId
        }).IsUnique();
        builder.Ignore(tombstone => tombstone.DomainEvents);
    }

    private static void ConfigureSha(
        PropertyBuilder<string> property) =>
        property
            .HasMaxLength(
                WorkspaceStaffCorrelationAnonymisationReceipt
                    .Sha256Length)
            .IsFixedLength()
            .IsRequired();
}
