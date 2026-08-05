namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Domain.Termination;
using BunkFy.Modules.Workspaces.Persistence;
using BunkFy.Modules.Workspaces.Persistence.TenantTermination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspaceTerminationFenceModelTests
{
    [Fact]
    public void Fence_and_receipt_are_tenant_filtered_and_constrained()
    {
        DbContextOptions<WorkspacesDbContext> options =
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        using WorkspacesDbContext context =
            new(options, new TestScopeContext());

        IEntityType fence = context.Model.FindEntityType(
            typeof(WorkspaceTerminationFence))!;
        Assert.True(fence.FindProperty(
            nameof(WorkspaceTerminationFence.Version))!.IsConcurrencyToken);
        Assert.NotEmpty(fence.GetDeclaredQueryFilters());
        IIndex active = Assert.Single(
            fence.GetIndexes(),
            index => string.Equals(
                index.GetDatabaseName(),
                "UX_workspace_termination_fences_active_scope",
                StringComparison.Ordinal));
        Assert.True(active.IsUnique);
        Assert.Equal("\"State\" IN (1, 2, 3)", active.GetFilter());

        IEntityType receipt = context.Model.FindEntityType(
            typeof(WorkspaceTerminationFenceReceipt))!;
        Assert.NotEmpty(receipt.GetDeclaredQueryFilters());
        Assert.Contains(
            receipt.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(WorkspaceTerminationFenceReceipt.ScopeId),
                        nameof(
                            WorkspaceTerminationFenceReceipt.IdempotencyKey)
                    ]));
        Assert.Contains(
            receipt.GetForeignKeys(),
            foreignKey =>
                foreignKey.PrincipalEntityType == fence &&
                foreignKey.Properties.Select(property => property.Name)
                    .SequenceEqual([
                        nameof(WorkspaceTerminationFenceReceipt.ScopeId),
                        nameof(WorkspaceTerminationFenceReceipt.FenceId)
                    ]));
    }

    [Fact]
    public void Tenant_destruction_progress_and_receipt_are_scoped_and_bound_to_terminal_proof()
    {
        DbContextOptions<WorkspacesDbContext> options =
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        using WorkspacesDbContext context =
            new(options, new TestScopeContext());

        IEntityType operation = context.Model.FindEntityType(
            typeof(WorkspaceTenantDestroyOperation))!;
        IEntityType receipt = context.Model.FindEntityType(
            typeof(WorkspaceTenantDestroyReceipt))!;
        IEntityType fence = context.Model.FindEntityType(
            typeof(WorkspaceTerminationFence))!;
        IEntityType fenceReceipt = context.Model.FindEntityType(
            typeof(WorkspaceTerminationFenceReceipt))!;

        Assert.NotEmpty(operation.GetDeclaredQueryFilters());
        Assert.True(operation.FindProperty(
            nameof(WorkspaceTenantDestroyOperation.ConcurrencyVersion))!
            .IsConcurrencyToken);
        Assert.Contains(operation.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(WorkspaceTenantDestroyOperation.ScopeId));
        Assert.Contains(operation.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType == fence &&
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(WorkspaceTenantDestroyOperation.ScopeId),
                    nameof(WorkspaceTenantDestroyOperation.FenceId)
                ]));

        Assert.NotEmpty(receipt.GetDeclaredQueryFilters());
        Assert.Contains(receipt.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Single().Name ==
                nameof(WorkspaceTenantDestroyReceipt.ScopeId));
        Assert.Contains(receipt.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType == fence &&
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(WorkspaceTenantDestroyReceipt.ScopeId),
                    nameof(WorkspaceTenantDestroyReceipt.FenceId)
                ]));
        Assert.Contains(receipt.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType == fenceReceipt &&
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual([
                    nameof(WorkspaceTenantDestroyReceipt.ScopeId),
                    nameof(WorkspaceTenantDestroyReceipt.CloseFenceReceiptId)
                ]));
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId =>
            "9a8f9c94-2dd7-42b4-9910-b00cb92f2a98";
    }
}
