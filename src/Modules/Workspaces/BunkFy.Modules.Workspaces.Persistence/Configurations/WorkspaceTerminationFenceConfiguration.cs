namespace BunkFy.Modules.Workspaces.Persistence.Configurations;

using BunkFy.Modules.Workspaces.Domain.Termination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

internal sealed class WorkspaceTerminationFenceConfiguration
    : IEntityTypeConfiguration<WorkspaceTerminationFence>
{
    public void Configure(
        EntityTypeBuilder<WorkspaceTerminationFence> builder)
    {
        builder.ToTable(
            "workspace_termination_fences",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_workspace_termination_fence_state",
                    "\"State\" BETWEEN 1 AND 4");
                table.HasCheckConstraint(
                    "CK_workspace_termination_fence_revisions",
                    "\"ApprovalRevision\" >= 1 AND \"Version\" >= 1");
                table.HasCheckConstraint(
                    "CK_workspace_termination_fence_policy_digest",
                    "char_length(\"PolicyEvidenceSha256\") = 64");
                table.HasCheckConstraint(
                    "CK_workspace_termination_fence_timestamps",
                    "\"CreatedAtUtc\" <= \"LastChangedAtUtc\"");
            });
        builder.HasKey(fence => fence.Id);
        builder.HasAlternateKey(fence => new { fence.ScopeId, fence.Id });
        builder.Property(fence => fence.ScopeId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(fence => fence.PolicyEvidenceSha256)
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(fence => fence.State)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(fence => fence.CreatedBy)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(fence => fence.LastChangedBy)
            .HasMaxLength(200)
            .IsRequired();
        builder.Property(fence => fence.Version)
            .IsConcurrencyToken();
        builder.HasIndex(fence => new { fence.ScopeId, fence.ProcessId })
            .IsUnique();
        builder.HasIndex(
                fence => new { fence.ScopeId, fence.TerminationEpoch })
            .IsUnique();
        builder.HasIndex(fence => fence.ScopeId)
            .HasDatabaseName(
                "UX_workspace_termination_fences_active_scope")
            .HasFilter("\"State\" IN (1, 2, 3)")
            .IsUnique();
        builder.Ignore(fence => fence.DomainEvents);
    }
}
