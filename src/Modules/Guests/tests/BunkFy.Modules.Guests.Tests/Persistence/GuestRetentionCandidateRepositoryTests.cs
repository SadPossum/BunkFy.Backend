namespace BunkFy.Modules.Guests.Tests.Persistence;

using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Guests.Persistence.Repositories;
using BunkFy.Modules.Guests.Tests.Application;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestRetentionCandidateRepositoryTests
{
    [Fact]
    public async Task Scan_is_bounded_aggregated_and_tenant_scoped()
    {
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase(
                    $"guest-retention-candidates-{Guid.NewGuid():N}")
                .Options;
        GuestRetentionPolicyFixture policy =
            GuestRetentionTestData.CreatePolicy();
        Guid tenantAGuestId = Guid.NewGuid();
        await SeedAsync(
            options,
            "tenant-a",
            tenantAGuestId,
            policy.Binding);
        await SeedAsync(
            options,
            "tenant-b",
            Guid.NewGuid(),
            policy.Binding);

        await using GuestsDbContext dbContext = new(
            options,
            new TestScopeContext("tenant-a"));
        GuestRetentionCandidateRepository repository = new(dbContext);

        GuestRetentionScanPage page = await repository.ScanAsync(
            afterProjectionOrdinal: 0,
            limit: 1,
            CancellationToken.None);

        GuestRetentionCandidateSnapshot candidate =
            Assert.Single(page.Candidates);
        GuestRetentionStaySnapshot stay = Assert.Single(candidate.Stays);
        GuestRetentionPropertySnapshot property =
            Assert.Single(candidate.Properties);
        Assert.Equal(tenantAGuestId, candidate.GuestId);
        Assert.True(page.ReachedEnd);
        Assert.True(stay.ProjectionSupported);
        Assert.False(stay.HasOperationalStay);
        Assert.Equal(new DateOnly(2025, 1, 4),
            stay.LatestTerminalBusinessDate);
        Assert.Equal("UTC", property.TimeZoneId);
        Assert.NotNull(property.GovernancePolicy);
    }

    private static async Task SeedAsync(
        DbContextOptions<GuestsDbContext> options,
        string tenantId,
        Guid guestId,
        PropertyGovernancePolicyBinding policy)
    {
        await using GuestsDbContext dbContext = new(
            options,
            new TestScopeContext(tenantId));
        Guid propertyId = Guid.NewGuid();
        GuestProfile profile = GuestProfile.Create(
            guestId,
            tenantId,
            propertyId,
            "Guest",
            legalName: null,
            email: null,
            phone: null,
            dateOfBirth: null,
            nationalityCountryCode: null,
            preferredLanguageTag: null,
            notes: null,
            "user:creator",
            Guid.NewGuid(),
            GuestRetentionTestData.Now.AddYears(-2)).Value;
        GuestPropertyProjection property = new(
            tenantId,
            propertyId,
            "Property",
            "UTC",
            PropertyStatus.Active,
            version: 1);
        property.ApplyPolicy(
            PropertyProcessingStatus.Enabled,
            policy,
            sourceVersion: 1);
        dbContext.GuestProfiles.Add(profile);
        dbContext.Entry(profile)
            .Property(item => item.ProjectionOrdinal)
            .CurrentValue = string.Equals(
                tenantId,
                "tenant-a",
                StringComparison.Ordinal)
                    ? 1
                    : 2;
        dbContext.PropertyProjections.Add(property);
        dbContext.StayHistory.Add(new(
            tenantId,
            profile.Id,
            Guid.NewGuid(),
            propertyId,
            GuestStayRole.Primary,
            new DateOnly(2025, 1, 2),
            new DateOnly(2025, 1, 4),
            GuestStayStatus.CheckedOut,
            new DateOnly(2025, 1, 2),
            noShowBusinessDate: null,
            new DateOnly(2025, 1, 4),
            isCurrentParticipant: false,
            reservationVersion: 1,
            GuestsModuleMetadata.StayHistoryProjectionVersion));
        await dbContext.SaveChangesAsync();
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }
}
