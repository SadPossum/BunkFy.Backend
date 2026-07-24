namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Guests.Persistence.Repositories;
using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestProcessingRestrictionProjectionRepositoryTests
{
    [Fact]
    public async Task Profile_and_current_stay_visibility_initialize_unrestricted_projections()
    {
        await using GuestsDbContext dbContext = CreateDbContext();
        TestScopeContext scopeContext = new();
        GuestProcessingRestrictionProjectionRepository projections =
            new(dbContext, scopeContext);
        GuestProfileRepository profiles = new(dbContext, projections);
        GuestStayHistoryRepository stays = new(dbContext, projections);
        Guid guestId = Guid.NewGuid();
        Guid originPropertyId = Guid.NewGuid();
        Guid stayPropertyId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = new(2026, 7, 24, 16, 0, 0, TimeSpan.Zero);
        GuestProfile profile = GuestProfile.Create(
            guestId,
            scopeContext.ScopeId,
            originPropertyId,
            "Guest",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "staff:test",
            Guid.NewGuid(),
            createdAtUtc).Value;

        await profiles.AddAsync(profile, CancellationToken.None);
        await stays.ApplyAsync(
            new GuestStayHistoryWriteModel(
                scopeContext.ScopeId,
                guestId,
                Guid.NewGuid(),
                stayPropertyId,
                GuestStayRole.Primary,
                new DateOnly(2026, 7, 24),
                new DateOnly(2026, 7, 25),
                GuestStayStatus.Confirmed,
                null,
                null,
                null,
                IsCurrentParticipant: true,
                ReservationVersion: 1,
                ObservedAtUtc: createdAtUtc.AddMinutes(1)),
            CancellationToken.None);
        await dbContext.SaveChangesAsync();

        var persisted = await dbContext.ProcessingRestrictionProjections
            .OrderBy(projection => projection.PropertyId)
            .ToArrayAsync();
        Assert.Equal(2, persisted.Length);
        Assert.All(persisted, projection =>
        {
            Assert.Equal(GuestProcessingRestrictionContract.CurrentVersion, projection.ContractVersion);
            Assert.False(projection.IsRestricted);
            Assert.Equal(0, projection.Revision);
            Assert.Equal(0, projection.ActiveRestrictionCount);
        });
        Assert.Contains(persisted, projection => projection.PropertyId == originPropertyId);
        Assert.Contains(persisted, projection => projection.PropertyId == stayPropertyId);
    }

    [Fact]
    public async Task Operational_visibility_fails_closed_while_data_rights_access_remains_available()
    {
        await using GuestsDbContext dbContext = CreateDbContext();
        TestScopeContext scopeContext = new();
        GuestProcessingRestrictionProjectionRepository projections =
            new(dbContext, scopeContext);
        GuestProfileRepository profiles = new(dbContext, projections);
        Guid propertyId = Guid.NewGuid();
        DateTimeOffset createdAtUtc = new(2026, 7, 24, 16, 30, 0, TimeSpan.Zero);
        GuestProfile unrestricted = CreateProfile(
            scopeContext.ScopeId,
            propertyId,
            "Unrestricted",
            createdAtUtc);
        GuestProfile restricted = CreateProfile(
            scopeContext.ScopeId,
            propertyId,
            "Restricted",
            createdAtUtc);
        GuestProfile missingProjection = CreateProfile(
            scopeContext.ScopeId,
            propertyId,
            "Missing projection",
            createdAtUtc);
        GuestProfile futureProjection = CreateProfile(
            scopeContext.ScopeId,
            propertyId,
            "Future projection",
            createdAtUtc);

        await profiles.AddAsync(unrestricted, CancellationToken.None);
        await profiles.AddAsync(restricted, CancellationToken.None);
        dbContext.GuestProfiles.AddRange(missingProjection, futureProjection);
        dbContext.ProcessingRestrictionProjections.Add(
            GuestProcessingRestrictionProjection.Create(
                scopeContext.ScopeId,
                propertyId,
                futureProjection.Id,
                GuestProcessingRestrictionContract.CurrentVersion + 1,
                createdAtUtc).Value);
        await dbContext.SaveChangesAsync();

        GuestProcessingRestrictionProjection restrictedProjection =
            await dbContext.ProcessingRestrictionProjections.SingleAsync(projection =>
                projection.PropertyId == propertyId &&
                projection.GuestId == restricted.Id);
        Assert.True(restrictedProjection.Apply(
            0,
            GuestProcessingRestrictionContract.CurrentVersion,
            createdAtUtc.AddMinutes(1)).IsSuccess);
        await dbContext.SaveChangesAsync();

        Assert.NotNull(await profiles.GetVisibleAsync(
            propertyId,
            unrestricted.Id,
            CancellationToken.None));
        Assert.Null(await profiles.GetVisibleAsync(
            propertyId,
            restricted.Id,
            CancellationToken.None));
        Assert.Null(await profiles.GetVisibleAsync(
            propertyId,
            missingProjection.Id,
            CancellationToken.None));
        Assert.Null(await profiles.GetVisibleAsync(
            propertyId,
            futureProjection.Id,
            CancellationToken.None));

        Assert.NotNull(await profiles.GetForDataRightsAsync(
            propertyId,
            restricted.Id,
            CancellationToken.None));
        Assert.NotNull(await profiles.GetForDataRightsAsync(
            propertyId,
            missingProjection.Id,
            CancellationToken.None));
        Assert.NotNull(await profiles.GetForDataRightsAsync(
            propertyId,
            futureProjection.Id,
            CancellationToken.None));

        GuestListResponse visible = await profiles.ListVisibleAsync(
            propertyId,
            null,
            null,
            new PageRequest(1, 20),
            CancellationToken.None);
        GuestProfileDto onlyVisible = Assert.Single(visible.Guests);
        Assert.Equal(unrestricted.Id, onlyVisible.GuestId);
    }

    private static GuestProfile CreateProfile(
        string scopeId,
        Guid propertyId,
        string displayName,
        DateTimeOffset createdAtUtc) => GuestProfile.Create(
        Guid.NewGuid(),
        scopeId,
        propertyId,
        displayName,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        "staff:test",
        Guid.NewGuid(),
        createdAtUtc).Value;

    private static GuestsDbContext CreateDbContext()
    {
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase($"guest-restriction-projections-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext());
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
