namespace BunkFy.Modules.Guests.Tests.Persistence;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Guests.Persistence.Repositories;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestDataRightsDiscoveryContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Exact_email_finds_a_historical_guest_with_masked_hints()
    {
        await using GuestsDbContext dbContext = CreateDbContext("tenant-a");
        Guid targetPropertyId = Guid.NewGuid();
        Guid originPropertyId = Guid.NewGuid();
        GuestProfile profile = CreateProfile(
            "tenant-a",
            originPropertyId,
            "Maya Chen",
            "maya.chen@example.test",
            "+44 20 1234 5678");
        dbContext.PropertyProjections.Add(new GuestPropertyProjection(
            "tenant-a",
            targetPropertyId,
            "Historical Property",
            PropertyStatus.Retired,
            1));
        dbContext.GuestProfiles.Add(profile);
        dbContext.StayHistory.Add(new GuestStayHistoryEntry(
            "tenant-a",
            profile.Id,
            Guid.NewGuid(),
            targetPropertyId,
            GuestStayRole.Primary,
            new DateOnly(2025, 1, 2),
            new DateOnly(2025, 1, 4),
            GuestStayStatus.CheckedOut,
            new DateOnly(2025, 1, 2),
            null,
            new DateOnly(2025, 1, 4),
            isCurrentParticipant: false,
            reservationVersion: 1));
        await dbContext.SaveChangesAsync();
        GuestDataRightsDiscoveryContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));

        DataRightsSubjectDiscoveryResult result = await contributor.DiscoverAsync(
            new DataRightsSubjectDiscoveryRequest(
                "tenant-a",
                targetPropertyId,
                new(null, "maya.chen@example.test", null, null, null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectDiscoveryStatus.Succeeded, result.Status);
        DataRightsSubjectCandidate candidate = Assert.Single(result.Candidates);
        Assert.Equal(profile.Id, candidate.Coordinate.RecordId);
        Assert.Equal("Maya Chen", candidate.DisplayName);
        Assert.Equal("m***@example.test", candidate.EmailHint);
        Assert.Equal("***5678", candidate.PhoneHint);
    }

    [Fact]
    public async Task Discovery_is_bounded_and_rejects_weak_or_unknown_scope()
    {
        await using GuestsDbContext dbContext = CreateDbContext("tenant-a");
        Guid propertyId = Guid.NewGuid();
        dbContext.PropertyProjections.Add(new GuestPropertyProjection(
            "tenant-a",
            propertyId,
            "Property",
            PropertyStatus.Active,
            1));
        for (int index = 0; index < 25; index++)
        {
            dbContext.GuestProfiles.Add(CreateProfile(
                "tenant-a",
                propertyId,
                $"Guest {index:D2}",
                "shared@example.test",
                null));
        }

        await dbContext.SaveChangesAsync();
        GuestDataRightsDiscoveryContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));

        DataRightsSubjectDiscoveryResult bounded = await contributor.DiscoverAsync(
            new DataRightsSubjectDiscoveryRequest(
                "tenant-a",
                propertyId,
                new(null, "shared@example.test", null, null, null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);
        DataRightsSubjectDiscoveryResult nameOnly = await contributor.DiscoverAsync(
            new DataRightsSubjectDiscoveryRequest(
                "tenant-a",
                propertyId,
                new(null, null, null, "Guest 01", null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);
        DataRightsSubjectDiscoveryResult wrongTenant = await contributor.DiscoverAsync(
            new DataRightsSubjectDiscoveryRequest(
                "tenant-b",
                propertyId,
                new(null, "shared@example.test", null, null, null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);
        DataRightsSubjectDiscoveryResult unknownProperty = await contributor.DiscoverAsync(
            new DataRightsSubjectDiscoveryRequest(
                "tenant-a",
                Guid.NewGuid(),
                new(null, "shared@example.test", null, null, null),
                DataRightsSubjectDiscoveryLimits.MaxCandidates),
            CancellationToken.None);

        Assert.Equal(DataRightsSubjectDiscoveryLimits.MaxCandidates, bounded.Candidates.Count);
        Assert.Equal(
            bounded.Candidates.OrderBy(candidate => candidate.Coordinate.RecordId)
                .Select(candidate => candidate.Coordinate.RecordId),
            bounded.Candidates.Select(candidate => candidate.Coordinate.RecordId));
        Assert.Equal(DataRightsSubjectDiscoveryStatus.ScopeUnavailable, nameOnly.Status);
        Assert.Equal(DataRightsSubjectDiscoveryStatus.ScopeUnavailable, wrongTenant.Status);
        Assert.Equal(DataRightsSubjectDiscoveryStatus.ScopeUnavailable, unknownProperty.Status);
    }

    [Fact]
    public async Task Selection_revalidation_rejects_stale_or_cross_property_coordinates()
    {
        await using GuestsDbContext dbContext = CreateDbContext("tenant-a");
        Guid propertyId = Guid.NewGuid();
        GuestProfile profile = CreateProfile(
            "tenant-a",
            propertyId,
            "Guest",
            "guest@example.test",
            null);
        dbContext.PropertyProjections.Add(new GuestPropertyProjection(
            "tenant-a",
            propertyId,
            "Property",
            PropertyStatus.Active,
            1));
        dbContext.GuestProfiles.Add(profile);
        await dbContext.SaveChangesAsync();
        GuestDataRightsDiscoveryContributor contributor =
            new(dbContext, new TestScopeContext("tenant-a"));

        DataRightsSubjectSelectionValidation valid = await contributor.ValidateSelectionAsync(
            new(
                "tenant-a",
                propertyId,
                new(
                    GuestDataRightsDiscoveryContributor.Owner,
                    GuestDataRightsDiscoveryContributor.ProfileRecordType,
                    profile.Id,
                    profile.Version)),
            CancellationToken.None);
        DataRightsSubjectSelectionValidation stale = await contributor.ValidateSelectionAsync(
            new(
                "tenant-a",
                propertyId,
                new(
                    GuestDataRightsDiscoveryContributor.Owner,
                    GuestDataRightsDiscoveryContributor.ProfileRecordType,
                    profile.Id,
                    profile.Version + 1)),
            CancellationToken.None);
        DataRightsSubjectSelectionValidation anotherProperty =
            await contributor.ValidateSelectionAsync(
                new(
                    "tenant-a",
                    Guid.NewGuid(),
                    new(
                        GuestDataRightsDiscoveryContributor.Owner,
                        GuestDataRightsDiscoveryContributor.ProfileRecordType,
                        profile.Id,
                        profile.Version)),
                CancellationToken.None);

        Assert.Equal(DataRightsSubjectSelectionValidationStatus.Valid, valid.Status);
        Assert.Equal(DataRightsSubjectSelectionValidationStatus.Stale, stale.Status);
        Assert.Equal(
            DataRightsSubjectSelectionValidationStatus.ScopeUnavailable,
            anotherProperty.Status);
    }

    private static GuestProfile CreateProfile(
        string tenantId,
        Guid originPropertyId,
        string displayName,
        string? email,
        string? phone) => GuestProfile.Create(
        Guid.NewGuid(),
        tenantId,
        originPropertyId,
        displayName,
        legalName: null,
        email,
        phone,
        dateOfBirth: null,
        nationalityCountryCode: null,
        preferredLanguageTag: null,
        notes: null,
        "user:test",
        Guid.NewGuid(),
        Now).Value;

    private static GuestsDbContext CreateDbContext(string tenantId)
    {
        DbContextOptions<GuestsDbContext> options =
            new DbContextOptionsBuilder<GuestsDbContext>()
                .UseInMemoryDatabase($"guests-data-rights-{Guid.NewGuid():N}")
                .Options;
        return new(options, new TestScopeContext(tenantId));
    }

    private sealed class TestScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId { get; } = scopeId;
    }
}
