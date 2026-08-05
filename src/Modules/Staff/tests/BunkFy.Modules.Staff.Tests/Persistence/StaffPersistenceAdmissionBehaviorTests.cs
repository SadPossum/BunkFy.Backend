namespace BunkFy.Modules.Staff.Tests.Persistence;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffPersistenceAdmissionBehaviorTests
{
    [Theory]
    [InlineData(1, "Staff.WorkspaceProcessingRestricted")]
    [InlineData(2, "Staff.WorkspaceProcessingAdmissionUnavailable")]
    public async Task Admission_failure_is_mapped_and_tracker_is_cleared(
        int failureValue,
        string expectedCode)
    {
        await using StaffDbContext context = CreateContext();
        context.StaffMembers.Add(CreateMember());
        StaffPersistenceAdmissionBehavior<TestCommand, string> behavior =
            new(context);
        StaffOperationalAdmissionFailure failure =
            (StaffOperationalAdmissionFailure)failureValue;

        Result<string> result = await behavior.HandleAsync(
            new TestCommand(),
            () => throw new StaffOperationalAdmissionException(failure),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Empty(context.ChangeTracker.Entries());
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
        "Maya Chen",
        null,
        "maya@example.test",
        null,
        null,
        null,
        null,
        null,
        "user:owner",
        Guid.NewGuid(),
        new DateTimeOffset(2026, 7, 31, 18, 0, 0, TimeSpan.Zero)).Value;

    private sealed record TestCommand : ICommand<string>;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
