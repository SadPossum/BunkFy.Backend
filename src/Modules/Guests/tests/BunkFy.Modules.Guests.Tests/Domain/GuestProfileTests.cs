namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Domain.Aggregates;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_normalizes_profile_and_actor_without_treating_contact_as_identity()
    {
        GuestProfile first = Create("ADA Guest", "Shared@Example.Test", "+1 555 0100");
        GuestProfile second = Create("Second Guest", "Shared@Example.Test", "+1 555 0100");

        Assert.Equal("ADA Guest", first.DisplayName);
        Assert.Equal("ADA GUEST", first.DisplayNameSearch);
        Assert.Equal("shared@example.test", first.Email);
        Assert.Equal("SHARED@EXAMPLE.TEST", first.EmailSearch);
        Assert.Equal("US", first.NationalityCountryCode);
        Assert.Equal("user:operator-a", first.CreatedBy);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(GuestProfileState.Active, first.Status);
        Assert.Equal(1, first.Version);
    }

    [Fact]
    public void Update_and_archive_are_versioned_and_archived_profiles_are_immutable()
    {
        GuestProfile profile = Create("Ada Guest", null, null);

        Assert.True(profile.Update(
            "Ada Lovelace",
            "Augusta Ada King",
            "ada@example.test",
            null,
            new DateOnly(1815, 12, 10),
            "gb",
            "en-GB",
            "Returning guest",
            expectedVersion: 1,
            "user:operator-b",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(2, profile.Version);
        Assert.Equal("user:operator-b", profile.LastChangedBy);

        Assert.Equal(
            "Guests.VersionConflict",
            profile.Archive(1, "user:operator-b", Guid.NewGuid(), Now.AddMinutes(2)).Error.Code);
        Assert.True(profile.Archive(
            2, "user:operator-b", Guid.NewGuid(), Now.AddMinutes(2)).IsSuccess);
        Assert.Equal(GuestProfileState.Archived, profile.Status);
        Assert.Equal(3, profile.Version);
        Assert.Equal(
            "Guests.GuestArchived",
            profile.Update(
                profile.DisplayName,
                profile.LegalName,
                profile.Email,
                profile.Phone,
                profile.DateOfBirth,
                profile.NationalityCountryCode,
                profile.PreferredLanguageTag,
                profile.Notes,
                profile.Version,
                "user:operator-c",
                Guid.NewGuid(),
                Now.AddMinutes(3)).Error.Code);
    }

    [Fact]
    public void Future_birth_date_and_invalid_provenance_are_rejected()
    {
        Assert.Equal(
            "Guests.DateOfBirthInvalid",
            GuestProfile.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                "Future Guest",
                null,
                null,
                null,
                new DateOnly(2027, 1, 1),
                null,
                null,
                null,
                "user:operator-a",
                Guid.NewGuid(),
                Now).Error.Code);
        Assert.Equal(
            "Guests.ActorInvalid",
            GuestProfile.Create(
                Guid.NewGuid(),
                "tenant-a",
                Guid.NewGuid(),
                "Guest",
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                " ",
                Guid.NewGuid(),
                Now).Error.Code);
    }

    private static GuestProfile Create(string displayName, string? email, string? phone) => GuestProfile.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        displayName,
        legalName: null,
        email,
        phone,
        new DateOnly(1990, 1, 1),
        "us",
        "en-US",
        notes: null,
        "  user:operator-a  ",
        Guid.NewGuid(),
        Now).Value;
}
