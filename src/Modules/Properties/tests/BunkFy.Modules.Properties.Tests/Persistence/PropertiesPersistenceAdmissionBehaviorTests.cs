namespace BunkFy.Modules.Properties.Tests;

using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PropertiesPersistenceAdmissionBehaviorTests
{
    [Theory]
    [InlineData(
        1,
        "Properties.WorkspaceProcessingRestricted")]
    [InlineData(
        2,
        "Properties.WorkspaceProcessingAdmissionUnavailable")]
    public async Task Admission_failure_is_mapped_and_tracker_is_cleared(
        int failureValue,
        string expectedCode)
    {
        await using PropertiesDbContext context = CreateContext();
        context.Properties.Add(Property.Create(
            Guid.NewGuid(),
            "tenant-a",
            "Hostel",
            "hostel",
            "UTC",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow).Value);
        PropertiesPersistenceAdmissionBehavior<TestCommand, string> behavior =
            new(context);
        PropertiesOperationalAdmissionFailure failure =
            (PropertiesOperationalAdmissionFailure)failureValue;

        Result<string> result = await behavior.HandleAsync(
            new TestCommand(),
            () => throw new PropertiesOperationalAdmissionException(failure),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static PropertiesDbContext CreateContext()
    {
        DbContextOptions<PropertiesDbContext> options =
            new DbContextOptionsBuilder<PropertiesDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new PropertiesDbContext(options, new TestScopeContext());
    }

    private sealed record TestCommand : ICommand<string>;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
