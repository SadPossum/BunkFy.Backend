namespace BunkFy.Modules.Workspaces.Tests;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspacesPersistenceRetryBehaviorTests
{
    [Fact]
    public async Task Fence_unique_conflict_reexecutes_once()
    {
        await using WorkspacesDbContext dbContext = CreateContext();
        WorkspacesPersistenceRetryBehavior<
            ApplyWorkspaceTerminationFenceCommand,
            WorkspaceTerminationFenceReceiptDto> behavior =
            new(dbContext, _ => true);
        ApplyWorkspaceTerminationFenceCommand command = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            1,
            1,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('a', 64),
            "operator-1");
        int attempts = 0;

        Task<Result<WorkspaceTerminationFenceReceiptDto>> Next()
        {
            attempts++;
            return attempts == 1
                ? throw new DbUpdateException(
                    "simulated unique conflict")
                : Task.FromResult(
                    Result.Failure<
                        WorkspaceTerminationFenceReceiptDto>(
                        WorkspaceTerminationApplicationErrors
                            .ActiveFenceConflict));
        }

        Result<WorkspaceTerminationFenceReceiptDto> result =
            await behavior.HandleAsync(
                command,
                Next,
                CancellationToken.None);

        Assert.Equal(2, attempts);
        Assert.Equal(
            WorkspaceTerminationApplicationErrors.ActiveFenceConflict,
            result.Error);
    }

    private static WorkspacesDbContext CreateContext()
    {
        DbContextOptions<WorkspacesDbContext> options =
            new DbContextOptionsBuilder<WorkspacesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new WorkspacesDbContext(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
