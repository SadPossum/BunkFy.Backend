namespace BunkFy.Modules.Staff.Tests;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Staff.Persistence.Repositories;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffPropertyAudienceReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Property_audience_contains_only_active_assigned_authenticated_staff()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        Guid propertyId = Guid.NewGuid();
        StaffMember included = Create("Active", "user-active");
        StaffMember suspended = Create("Suspended", "user-suspended");
        StaffMember unlinked = Create("Unlinked", null);
        StaffMember otherProperty = Create("Elsewhere", "user-elsewhere");
        StaffMember restricted = Create("Restricted", "user-restricted");
        Assign(included, propertyId);
        Assign(suspended, propertyId);
        Assign(unlinked, propertyId);
        Assign(otherProperty, Guid.NewGuid());
        Assign(restricted, propertyId);
        suspended.Suspend(suspended.Version, "user:owner", "Leave", Guid.NewGuid(), Now.AddMinutes(1));
        dbContext.StaffMembers.AddRange(
            included,
            suspended,
            unlinked,
            otherProperty,
            restricted);
        dbContext.ProcessingRestrictionProjections.AddRange(
            CreateRestrictionProjection(included),
            CreateRestrictionProjection(suspended),
            CreateRestrictionProjection(unlinked),
            CreateRestrictionProjection(otherProperty),
            CreateRestrictionProjection(restricted));
        StaffProcessingRestrictionProjection restrictedProjection =
            dbContext.ProcessingRestrictionProjections.Local.Single(
                projection =>
                    projection.StaffMemberId == restricted.Id);
        Assert.True(restrictedProjection.Apply(
            0,
            StaffProcessingRestrictionContract.CurrentVersion,
            Now.AddMinutes(1)).IsSuccess);
        await dbContext.SaveChangesAsync();
        var reader = new StaffPropertyAudienceReader(dbContext);

        IReadOnlyList<string> recipients = await reader.ListActiveAuthSubjectIdsAsync(
            "tenant-a",
            propertyId,
            CancellationToken.None);

        Assert.Equal(["user-active"], recipients);
        Assert.Equal(
            "user-suspended",
            await reader.GetAuthSubjectIdAsync("tenant-a", suspended.Id, CancellationToken.None));
        Assert.Null(
            await reader.GetAuthSubjectIdAsync("tenant-a", restricted.Id, CancellationToken.None));
    }

    private static StaffDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<StaffDbContext>()
            .UseInMemoryDatabase($"staff-audience-{Guid.NewGuid():N}")
            .Options,
        new TestScopeContext());

    private static StaffMember Create(string name, string? authSubjectId) =>
        StaffMember.Create(
            Guid.NewGuid(),
            "tenant-a",
            name,
            null,
            null,
            null,
            null,
            null,
            null,
            authSubjectId,
            "user:owner",
            Guid.NewGuid(),
            Now).Value;

    private static void Assign(StaffMember member, Guid propertyId) =>
        member.AssignProperty(
            Guid.NewGuid(),
            propertyId,
            null,
            false,
            new DateOnly(2026, 7, 1),
            member.Version,
            "user:owner",
            Guid.NewGuid(),
            Now);

    private static StaffProcessingRestrictionProjection
        CreateRestrictionProjection(StaffMember member) =>
        StaffProcessingRestrictionProjection.Create(
            member.ScopeId,
            member.Id,
            StaffProcessingRestrictionContract.CurrentVersion,
            member.CreatedAtUtc).Value;

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
