namespace BunkFy.Modules.Guests.Tests.Persistence;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Guests.Persistence.Repositories;
using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestProfileRepositoryTests
{
    [Fact]
    public async Task Anonymised_profile_is_internal_only_across_ordinary_repository_queries()
    {
        TestScopeContext scopeContext = new();
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase($"guest-profile-repository-{Guid.NewGuid():N}")
                .Options;
        await using GuestsDbContext dbContext = new(options, scopeContext);
        GuestProcessingRestrictionProjectionRepository restrictions =
            new(dbContext, scopeContext);
        GuestProfileRepository repository = new(dbContext, restrictions);
        Guid propertyId = Guid.NewGuid();
        GuestProfile profile = GuestProfile.Create(
            Guid.NewGuid(),
            scopeContext.ScopeId,
            propertyId,
            "Guest",
            "Guest Legal",
            "guest@example.test",
            "+1 555 0100",
            new DateOnly(1990, 1, 1),
            "US",
            "en-US",
            "Notes",
            "user:creator",
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 25, 10, 0, 0, TimeSpan.Zero)).Value;
        await repository.AddAsync(profile, CancellationToken.None);
        await dbContext.SaveChangesAsync();
        Assert.True(profile.Anonymise(
            profile.Version,
            "user:privacy",
            Guid.NewGuid(),
            new DateTimeOffset(2026, 7, 25, 11, 0, 0, TimeSpan.Zero)).IsSuccess);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        GuestProfile? visible = await repository.GetVisibleAsync(
            propertyId,
            profile.Id,
            CancellationToken.None);
        GuestListResponse listed = await repository.ListVisibleAsync(
            propertyId,
            search: null,
            status: null,
            PageRequest.Normalize(1, 25),
            CancellationToken.None);
        GuestProfile? internalProfile = await repository.GetForDataRightsAsync(
            propertyId,
            profile.Id,
            CancellationToken.None);

        Assert.Null(visible);
        Assert.Empty(listed.Guests);
        Assert.NotNull(internalProfile);
        Assert.Equal(GuestProfileState.Anonymised, internalProfile.Status);
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }
}
