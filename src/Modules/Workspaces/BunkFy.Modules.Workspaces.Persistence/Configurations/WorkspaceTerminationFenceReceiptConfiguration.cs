namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain.Termination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkspaceTerminationFenceReceiptConfiguration
    : IEntityTypeConfiguration<WorkspaceTerminationFenceReceipt>
{
    public void Configure(
        EntityTypeBuilder<WorkspaceTerminationFenceReceipt> builder)
    {
        builder.ToTable(
            "workspace_termination_fence_receipts",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_workspace_termination_receipt_revisions",
                    "\"ApprovalRevision\" >= 1 AND " +
                    "\"OperationRevision\" >= 1 AND " +
                    "((\"Action\" = 1 AND " +
                    "\"SelectedFenceVersion\" = 0 AND " +
                    "\"ResultingFenceVersion\" = 1 AND " +
                    "\"ResultingState\" = 1) OR " +
                    "(\"Action\" = 2 AND " +
                    "\"SelectedFenceVersion\" >= 1 AND " +
                    "\"ResultingFenceVersion\" = " +
                    "\"SelectedFenceVersion\" + 1 AND " +
                    "\"ResultingState\" = 2) OR " +
                    "(\"Action\" = 3 AND " +
                    "\"SelectedFenceVersion\" >= 2 AND " +
                    "\"ResultingFenceVersion\" = " +
                    "\"SelectedFenceVersion\" + 1 AND " +
                    "\"ResultingState\" = 3) OR " +
                    "(\"Action\" = 4 AND " +
                    "\"SelectedFenceVersion\" >= 1 AND " +
                    "\"ResultingFenceVersion\" = " +
                    "\"SelectedFenceVersion\" + 1 AND " +
                    "\"ResultingState\" = 4))");
                table.HasCheckConstraint(
                    "CK_workspace_termination_receipt_policy_digest",
                    "char_length(\"PolicyEvidenceSha256\") = 64");
            });
        builder.HasKey(receipt => receipt.Id);
        builder.HasAlternateKey(
            receipt => new { receipt.ScopeId, receipt.Id });
        builder.Property(receipt => receipt.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(receipt => receipt.Action)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.ResultingState)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(receipt => receipt.PolicyEvidenceSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(receipt => receipt.ActorId)
            .HasMaxLength(200)
            .IsRequired();
        builder.HasIndex(
                receipt => new { receipt.ScopeId, receipt.IdempotencyKey })
            .IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ProcessId,
            receipt.Action,
            receipt.OperationRevision
        }).IsUnique();
        builder.HasIndex(receipt => new
        {
            receipt.ScopeId,
            receipt.ProcessId,
            receipt.CompletedAtUtc
        });
        builder.HasOne<WorkspaceTerminationFence>()
            .WithMany()
            .HasForeignKey(receipt => new
            {
                receipt.ScopeId,
                receipt.FenceId
            })
            .HasPrincipalKey(fence => new { fence.ScopeId, fence.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(receipt => receipt.DomainEvents);
    }
}
