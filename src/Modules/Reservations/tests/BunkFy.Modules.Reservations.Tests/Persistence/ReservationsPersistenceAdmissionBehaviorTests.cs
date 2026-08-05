namespace BunkFy.Modules.Reservations.Tests.Persistence;

using BunkFy.Modules.Reservations.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationsPersistenceAdmissionBehaviorTests
{
    [Theory]
    [InlineData(1, "Reservations.WorkspaceProcessingRestricted")]
    [InlineData(2, "Reservations.WorkspaceProcessingAdmissionUnavailable")]
    public async Task Admission_failure_is_mapped_and_tracker_is_cleared(
        int failureValue,
        string expectedCode)
    {
        await using ReservationsDbContext context = CreateContext();
        context.Reservations.Add(
            ReservationsTenantTerminationTestData.CreateReservation());
        ReservationsPersistenceAdmissionBehavior<TestCommand, string>
            behavior = new(context);
        ReservationsOperationalAdmissionFailure failure =
            (ReservationsOperationalAdmissionFailure)failureValue;

        Result<string> result = await behavior.HandleAsync(
            new TestCommand(),
            () => throw new ReservationsOperationalAdmissionException(
                failure),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static ReservationsDbContext CreateContext()
    {
        DbContextOptions<ReservationsDbContext> options =
            new DbContextOptionsBuilder<ReservationsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new ReservationsDbContext(
            options,
            new TestScopeContext());
    }

    private sealed record TestCommand : ICommand<string>;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId =>
            ReservationsTenantTerminationTestData.TenantId;
    }
}
