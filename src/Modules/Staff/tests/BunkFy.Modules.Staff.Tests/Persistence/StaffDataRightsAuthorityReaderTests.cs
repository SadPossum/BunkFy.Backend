namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Staff.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffDataRightsAuthorityReaderTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reader_exposes_only_exact_staff_authority_state()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        StaffMember staff = StaffMember.Create(
            Guid.NewGuid(),
            "tenant-a",
            "Departed Staff",
            null,
            null,
            null,
            null,
            null,
            null,
            "private-auth-subject",
            "user:owner",
            Guid.NewGuid(),
            Now).Value;
        Assert.True(staff.Depart(
            new DateOnly(2026, 7, 31),
            staff.Version,
            "user:owner",
            "Employment ended",
            Guid.NewGuid(),
            [],
            Now.AddMinutes(1)).IsSuccess);
        dbContext.StaffMembers.Add(staff);
        await dbContext.SaveChangesAsync();
        var reader = new StaffDataRightsAuthorityReader(dbContext);

        StaffDataRightsAuthorityState? state =
            await reader.ReadAsync(
                "tenant-a",
                staff.Id,
                CancellationToken.None);
        StaffDataRightsAuthorityState exact =
            Assert.IsType<StaffDataRightsAuthorityState>(state);
        Assert.Equal(staff.Id, exact.StaffMemberId);
        Assert.Equal(staff.Version, exact.Version);
        Assert.Equal(
            StaffDataRightsAuthorityRecordState.Departed,
            exact.State);
        Assert.Null(
            await reader.ReadAsync(
                "tenant-a",
                Guid.NewGuid(),
                CancellationToken.None));
    }

    private static StaffDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<StaffDbContext>()
            .UseInMemoryDatabase(
                $"staff-data-rights-authority-{Guid.NewGuid():N}")
            .Options,
        new TestScopeContext());

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
