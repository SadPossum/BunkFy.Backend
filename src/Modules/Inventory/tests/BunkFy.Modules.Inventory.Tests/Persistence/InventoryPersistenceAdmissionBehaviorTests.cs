namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.Inventory.Application;
using BunkFy.Modules.Inventory.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class InventoryPersistenceAdmissionBehaviorTests
{
    [Theory]
    [InlineData(1, "Inventory.WorkspaceProcessingRestricted")]
    [InlineData(2, "Inventory.WorkspaceProcessingAdmissionUnavailable")]
    public async Task Admission_failure_is_mapped_and_tracker_is_cleared(
        int failureValue,
        string expectedCode)
    {
        await using InventoryDbContext context = CreateContext();
        context.InventoryUnits.Add(InventoryUnit.CreateBed(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid()));
        InventoryPersistenceAdmissionBehavior<TestCommand, string> behavior =
            new(context);
        InventoryOperationalAdmissionFailure failure =
            (InventoryOperationalAdmissionFailure)failureValue;

        Result<string> result = await behavior.HandleAsync(
            new TestCommand(),
            () => throw new InventoryOperationalAdmissionException(failure),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static InventoryDbContext CreateContext()
    {
        DbContextOptions<InventoryDbContext> options =
            new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new InventoryDbContext(options, new TestScopeContext());
    }

    private sealed record TestCommand : ICommand<string>;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
