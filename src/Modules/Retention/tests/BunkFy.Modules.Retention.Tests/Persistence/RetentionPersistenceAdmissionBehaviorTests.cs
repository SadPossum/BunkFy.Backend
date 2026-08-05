namespace BunkFy.Modules.Retention.Tests.Persistence;

using BunkFy.Modules.Retention.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetentionPersistenceAdmissionBehaviorTests
{
    [Theory]
    [InlineData(1, "Retention.WorkspaceProcessingRestricted")]
    [InlineData(2, "Retention.WorkspaceProcessingAdmissionUnavailable")]
    public async Task Admission_failure_is_mapped_and_tracker_is_cleared(
        int failureValue,
        string expectedCode)
    {
        await using RetentionDbContext context = CreateContext();
        context.TenantProjections.Add(new(
            "tenant-a",
            Guid.NewGuid(),
            isActive: true,
            sourceVersion: 1));
        RetentionPersistenceAdmissionBehavior<TestCommand, string> behavior =
            new(context);
        RetentionOperationalAdmissionFailure failure =
            (RetentionOperationalAdmissionFailure)failureValue;

        Result<string> result = await behavior.HandleAsync(
            new TestCommand(),
            () => throw new RetentionOperationalAdmissionException(failure),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static RetentionDbContext CreateContext()
    {
        DbContextOptions<RetentionDbContext> options =
            new DbContextOptionsBuilder<RetentionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed record TestCommand : ICommand<string>;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
