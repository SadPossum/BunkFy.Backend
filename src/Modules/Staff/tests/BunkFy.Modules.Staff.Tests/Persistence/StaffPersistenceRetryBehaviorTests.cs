namespace BunkFy.Modules.Staff.Tests.Persistence;

using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffPersistenceRetryBehaviorTests
{
    [Theory]
    [InlineData(typeof(CreateStaffMemberCommand))]
    [InlineData(typeof(UpdateStaffMemberCommand))]
    [InlineData(typeof(UpdateCurrentStaffMemberCommand))]
    [InlineData(typeof(SetStaffAuthSubjectCommand))]
    [InlineData(typeof(ProvisionStaffOnboardingCommand))]
    [InlineData(typeof(ReconcileStaffIdentityCommand))]
    public void Profile_identity_commands_are_explicitly_retryable(
        Type commandType) => Assert.True(
        typeof(IStaffPersistenceRetryableCommand).IsAssignableFrom(commandType));

    [Fact]
    public async Task Retryable_command_retries_one_unique_conflict_with_a_clean_tracker()
    {
        await using StaffDbContext context = CreateContext();
        StaffPersistenceRetryBehavior<RetryableCommand, string> behavior =
            new(context, _ => true);
        int attempts = 0;

        Result<string> result = await behavior.HandleAsync(
            new RetryableCommand(),
            () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    context.StaffMembers.Add(CreateMember());
                    throw new DbUpdateException("simulated unique conflict");
                }

                Assert.Empty(context.ChangeTracker.Entries());
                return Task.FromResult(Result.Success("accepted"));
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.Equal("accepted", result.Value);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task Non_retryable_command_does_not_replay_a_unique_conflict()
    {
        await using StaffDbContext context = CreateContext();
        StaffPersistenceRetryBehavior<NonRetryableCommand, string> behavior =
            new(context, _ => true);
        int attempts = 0;

        await Assert.ThrowsAsync<DbUpdateException>(() => behavior.HandleAsync(
            new NonRetryableCommand(),
            () =>
            {
                attempts++;
                throw new DbUpdateException("simulated unique conflict");
            },
            CancellationToken.None));

        Assert.Equal(1, attempts);
    }

    private static StaffDbContext CreateContext()
    {
        DbContextOptions<StaffDbContext> options =
            new DbContextOptionsBuilder<StaffDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new StaffDbContext(options, new TestScopeContext());
    }

    private static StaffMember CreateMember() => StaffMember.Create(
        Guid.NewGuid(),
        "tenant-a",
        "Retry Probe",
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        "test:retry",
        Guid.NewGuid(),
        new DateTimeOffset(2026, 8, 6, 12, 0, 0, TimeSpan.Zero)).Value;

    private sealed record RetryableCommand : ICommand<string>,
        IStaffPersistenceRetryableCommand;

    private sealed record NonRetryableCommand : ICommand<string>;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
