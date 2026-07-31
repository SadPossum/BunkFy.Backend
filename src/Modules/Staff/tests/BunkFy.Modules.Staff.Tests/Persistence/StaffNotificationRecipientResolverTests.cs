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
public sealed class StaffNotificationRecipientResolverTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Resolver_returns_only_active_unrestricted_exact_candidates()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        StaffMember active = Create("Active", "user-active");
        StaffMember suspended = Create("Suspended", "user-suspended");
        StaffMember restricted = Create("Restricted", "user-restricted");
        StaffMember unrelated = Create("Unrelated", "user-unrelated");
        Assert.True(suspended.Suspend(
            suspended.Version,
            "user:owner",
            "Leave",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        dbContext.StaffMembers.AddRange(
            active,
            suspended,
            restricted,
            unrelated);
        dbContext.ProcessingRestrictionProjections.AddRange(
            CreateRestrictionProjection(active),
            CreateRestrictionProjection(suspended),
            CreateRestrictionProjection(restricted),
            CreateRestrictionProjection(unrelated));
        StaffProcessingRestrictionProjection restrictedProjection =
            dbContext.ProcessingRestrictionProjections.Local.Single(
                projection =>
                    projection.StaffMemberId == restricted.Id);
        Assert.True(restrictedProjection.Apply(
            0,
            StaffProcessingRestrictionContract.CurrentVersion,
            Now.AddMinutes(1)).IsSuccess);
        await dbContext.SaveChangesAsync();
        var resolver =
            new StaffNotificationRecipientResolver(dbContext);

        IReadOnlyList<StaffNotificationRecipient> recipients =
            await resolver.ResolveActiveAsync(
                "tenant-a",
                ["user-restricted", "user-active", "user-suspended"],
                CancellationToken.None);

        StaffNotificationRecipient recipient =
            Assert.Single(recipients);
        Assert.Equal(active.Id, recipient.StaffMemberId);
        Assert.Equal("user-active", recipient.AuthSubjectId);
    }

    [Fact]
    public async Task Resolver_rejects_an_oversized_candidate_batch()
    {
        await using StaffDbContext dbContext = CreateDbContext();
        var resolver =
            new StaffNotificationRecipientResolver(dbContext);
        string[] candidates = Enumerable.Range(
                0,
                StaffNotificationRecipientContract.MaximumCandidateCount + 1)
            .Select(index => $"user-{index:D4}")
            .ToArray();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            resolver.ResolveActiveAsync(
                "tenant-a",
                candidates,
                CancellationToken.None));
    }

    private static StaffDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<StaffDbContext>()
            .UseInMemoryDatabase(
                $"staff-notification-recipients-{Guid.NewGuid():N}")
            .Options,
        new TestScopeContext());

    private static StaffMember Create(
        string name,
        string authSubjectId) =>
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
