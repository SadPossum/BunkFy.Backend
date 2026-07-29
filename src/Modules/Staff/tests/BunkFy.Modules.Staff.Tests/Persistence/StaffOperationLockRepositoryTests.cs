namespace BunkFy.Modules.Staff.Tests.Persistence;

using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Staff.Persistence.Models;
using BunkFy.Modules.Staff.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffOperationLockRepositoryTests
{
    [Fact]
    public void Lock_coordinate_must_use_the_staff_member_id()
    {
        Guid staffMemberId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            new StaffOperationLock(
                Guid.NewGuid(),
                "tenant-a",
                staffMemberId));
    }

    [Fact]
    public async Task In_memory_fallback_advances_the_provisioned_scoped_row()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        StaffOperationLockRepository repository = new(dbContext);
        StaffMember member = CreateMember();
        Guid staffMemberId = member.Id;
        dbContext.StaffMembers.Add(member);
        dbContext.OperationLocks.Add(
            new StaffOperationLock(
                staffMemberId,
                "tenant-a",
                staffMemberId));
        await dbContext.SaveChangesAsync();

        Assert.True(await repository.TryAcquireStaffMemberAsync(
            "tenant-a",
            staffMemberId,
            CancellationToken.None));
        Assert.True(await repository.TryAcquireStaffMemberAsync(
            "tenant-a",
            staffMemberId,
            CancellationToken.None));

        StaffOperationLock resourceLock =
            await dbContext.OperationLocks.SingleAsync();
        Assert.Equal(staffMemberId, resourceLock.StaffMemberId);
        Assert.Equal("tenant-a", resourceLock.ScopeId);
        Assert.Equal(3, resourceLock.Revision);
    }

    [Fact]
    public async Task Unknown_staff_member_returns_false()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        StaffOperationLockRepository repository = new(dbContext);

        Assert.False(await repository.TryAcquireStaffMemberAsync(
            "tenant-a",
            Guid.NewGuid(),
            CancellationToken.None));
        Assert.Empty(dbContext.OperationLocks);
    }

    [Fact]
    public async Task Existing_staff_without_a_lock_is_rejected_as_corrupt()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        StaffMember member = CreateMember();
        dbContext.StaffMembers.Add(member);
        await dbContext.SaveChangesAsync();
        StaffOperationLockRepository repository = new(dbContext);

        InvalidOperationException error =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repository.TryAcquireStaffMemberAsync(
                    "tenant-a",
                    member.Id,
                    CancellationToken.None));

        Assert.Equal(
            "The Staff operation lock is not provisioned.",
            error.Message);
    }

    [Fact]
    public async Task Cross_scope_coordinate_is_rejected_before_persistence()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        StaffOperationLockRepository repository = new(dbContext);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.TryAcquireStaffMemberAsync(
                "tenant-b",
                Guid.NewGuid(),
                CancellationToken.None));

        Assert.Empty(dbContext.OperationLocks);
    }

    private static StaffDbContext CreateDbContext() =>
        new(
            new DbContextOptionsBuilder<StaffDbContext>()
                .UseInMemoryDatabase(
                    $"staff-operation-lock-{Guid.NewGuid():N}")
                .Options,
            new TestScopeContext());

    private static StaffMember CreateMember() =>
        StaffMember.Create(
            Guid.NewGuid(),
            "tenant-a",
            "Lock integrity",
            legalName: null,
            workEmail: null,
            workPhone: null,
            employeeNumber: null,
            jobTitle: null,
            department: null,
            authSubjectId: null,
            "user:creator",
            Guid.NewGuid(),
            new DateTimeOffset(
                2026,
                7,
                29,
                12,
                0,
                0,
                TimeSpan.Zero)).Value;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
