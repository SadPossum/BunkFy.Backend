namespace BunkFy.Modules.Inventory.Persistence.Configurations;

using BunkFy.Modules.Inventory.Domain.Aggregates;
using BunkFy.Modules.Inventory.Domain.DataRights;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class
    InventoryAllocationAnonymisationReceiptConfiguration
    : IEntityTypeConfiguration<
        InventoryAllocationAnonymisationReceipt>
{
    public void Configure(
        EntityTypeBuilder<
            InventoryAllocationAnonymisationReceipt> builder)
    {
        builder.ToTable(
            "allocation_anonymisation_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_receipts_contract",
                    $"\"ContractVersion\" = {InventoryAllocationAnonymisationReceipt.CurrentContractVersion}");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_receipts_revisions",
                    "\"ApprovalRevision\" >= 1 AND " +
                    "\"OperationRevision\" > \"ApprovalRevision\"");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_receipts_versions",
                    "\"SelectedAllocationVersion\" >= 1 AND " +
                    "\"ResultingAllocationVersion\" = " +
                    "\"SelectedAllocationVersion\" + 1");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_receipts_outcome",
                    $"\"Disposition\" = {(int)InventoryAllocationAnonymisationDisposition.Completed} AND " +
                    $"\"Reason\" = {(int)InventoryAllocationAnonymisationReason.ReservationCorrelationPseudonymised}");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_receipts_counts",
                    "\"RemovedAmendmentDecisionCount\" >= 0");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_receipts_digests",
                    $"char_length(\"ApprovalEvidenceSha256\") = {InventoryAllocationAnonymisationReceipt.Sha256Length} AND " +
                    $"char_length(\"CanonicalSha256\") = {InventoryAllocationAnonymisationReceipt.Sha256Length}");
                table.HasCheckConstraint(
                    "CK_allocation_anonymisation_receipts_actor",
                    "length(trim(\"ActorId\")) > 0");
            });

        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.Id
        });
        builder.HasAlternateKey(receipt => new
        {
            receipt.ScopeId,
            receipt.CanonicalSha256
        });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.Disposition)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.Reason)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.ApprovalEvidenceSha256)
            .HasMaxLength(
                InventoryAllocationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.CanonicalSha256)
            .HasMaxLength(
                InventoryAllocationAnonymisationReceipt.Sha256Length)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(
                InventoryAllocationAnonymisationReceipt
                    .ActorIdMaxLength)
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
            receipt.AllocationId
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.PropertyId,
            receipt.AllocationId,
            receipt.ResultingAllocationVersion
        }).IsUnique();
        builder.HasOne<InventoryAllocation>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.AllocationId
            })
            .HasPrincipalKey(allocation => new
            {
                allocation.ScopeId,
                allocation.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
