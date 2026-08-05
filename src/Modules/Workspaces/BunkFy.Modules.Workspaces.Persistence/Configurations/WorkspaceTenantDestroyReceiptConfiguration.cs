namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain.Termination;
using BunkFy.Modules.Workspaces.Persistence.TenantTermination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkspaceTenantDestroyReceiptConfiguration
    : IEntityTypeConfiguration<WorkspaceTenantDestroyReceipt>
{
    public void Configure(
        EntityTypeBuilder<WorkspaceTenantDestroyReceipt> builder)
    {
        builder.ToTable("tenant_destroy_receipts", table =>
        {
            table.HasCheckConstraint(
                "CK_workspaces_tenant_destroy_receipt_revisions",
                "\"SelectedFenceVersion\" >= 1 AND " +
                "\"ResultingFenceVersion\" = " +
                "\"SelectedFenceVersion\" + 2");
            table.HasCheckConstraint(
                "CK_workspaces_tenant_destroy_receipt_progress",
                "((\"RemovedRecordCount\" = 0 AND " +
                "\"CompletedBatchCount\" = 0) OR " +
                "(\"RemovedRecordCount\" > 0 AND " +
                "\"CompletedBatchCount\" > 0)) AND " +
                $"\"BatchSize\" BETWEEN 1 AND " +
                $"{WorkspaceTenantDestroyOperation.MaximumBatchSize} AND " +
                "\"RemovalProofVersion\" = 1");
            table.HasCheckConstraint(
                "CK_workspaces_tenant_destroy_receipt_times",
                "\"CompletedAtUtc\" >= \"StartedAtUtc\"");
        });
        builder.HasKey(receipt => receipt.OperationId);
        builder.Property(receipt => receipt.OperationId)
            .ValueGeneratedNever();
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.RequestSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.RemovalProofSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.HasIndex(receipt => receipt.ScopeId).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.CloseFenceReceiptId
        }).IsUnique();
        builder.HasOne<WorkspaceTerminationFence>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.FenceId
            })
            .HasPrincipalKey(fence => new { fence.ScopeId, fence.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkspaceTerminationFenceReceipt>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                Id = receipt.CloseFenceReceiptId
            })
            .HasPrincipalKey(fenceReceipt => new
            {
                fenceReceipt.ScopeId,
                fenceReceipt.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
