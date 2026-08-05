namespace BunkFy.Modules.Guests.Tests.Persistence;

using BunkFy.Modules.Guests.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestsPersistenceAdmissionBehaviorTests
{
    [Theory]
    [InlineData(1, "Guests.WorkspaceProcessingRestricted")]
    [InlineData(2, "Guests.WorkspaceProcessingAdmissionUnavailable")]
    public async Task Admission_failure_is_mapped_and_tracker_is_cleared(
        int failureValue,
        string expectedCode)
    {
        await using GuestsDbContext context = CreateContext();
        context.GuestProfiles.Add(
            GuestsTenantTerminationTestData.CreateProfile());
        GuestsPersistenceAdmissionBehavior<TestCommand, string> behavior =
            new(context);
        GuestsOperationalAdmissionFailure failure =
            (GuestsOperationalAdmissionFailure)failureValue;

        Result<string> result = await behavior.HandleAsync(
            new TestCommand(),
            () => throw new GuestsOperationalAdmissionException(failure),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static GuestsDbContext CreateContext()
    {
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new GuestsDbContext(options, new TestScopeContext());
    }

    private sealed record TestCommand : ICommand<string>;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId =>
            GuestsTenantTerminationTestData.TenantId;
    }
}
