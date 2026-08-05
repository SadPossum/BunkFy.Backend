namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using BunkFy.Modules.Ingestion.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionPersistenceAdmissionBehaviorTests
{
    [Theory]
    [InlineData(1, "Ingestion.TenantLifecycleRestricted")]
    [InlineData(2, "Ingestion.TenantLifecycleAdmissionUnavailable")]
    public async Task Admission_failure_is_mapped_and_tracker_is_cleared(
        int failureValue,
        string expectedCode)
    {
        await using IngestionDbContext context = CreateContext();
        context.Add(IngestionTenantRevision.Create(
            "10000000-0000-0000-0000-000000000001"));
        IngestionPersistenceAdmissionBehavior<TestCommand, string>
            behavior = new(context);
        IngestionOperationalAdmissionFailure failure =
            (IngestionOperationalAdmissionFailure)failureValue;

        Result<string> result = await behavior.HandleAsync(
            new TestCommand(),
            () => throw new IngestionOperationalAdmissionException(failure),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static IngestionDbContext CreateContext()
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new IngestionDbContext(options, new TestScopeContext());
    }

    private sealed record TestCommand : ICommand<string>;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId =>
            "10000000-0000-0000-0000-000000000001";
    }
}
