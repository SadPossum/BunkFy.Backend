namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.Models;
using BunkFy.Modules.Guests.Domain.ValueObjects;
using Gma.Framework.Results;
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

    [Fact]
    public void Update_outcome_reports_normalized_changed_fields_without_personal_values()
    {
        GuestProfile profile = Create("Ada Guest", "ada@example.test", "+1 555 0100");
        Guid eventId = Guid.NewGuid();
        DateTimeOffset changedAtUtc = Now.AddMinutes(1);

        Result<GuestProfileUpdateOutcome> result = profile.UpdateWithOutcome(
            "  Ada Guest  ",
            null,
            "NEW@example.test",
            "  +1 555 0100  ",
            profile.DateOfBirth,
            "us",
            "en-US",
            "Returning guest",
            profile.Version,
            "user:operator-b",
            eventId,
            changedAtUtc);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.PreviousVersion);
        Assert.Equal(2, result.Value.CurrentVersion);
        Assert.Equal(eventId, result.Value.EventId);
        Assert.Equal(changedAtUtc, result.Value.OccurredAtUtc);
        Assert.Equal(
            [GuestProfileField.Email, GuestProfileField.Notes],
            result.Value.ChangedFields);
        Assert.Equal("new@example.test", profile.Email);
        Assert.DoesNotContain(
            typeof(GuestProfileUpdateOutcome).GetProperties(),
            property => property.Name is
                "DisplayName" or
                "LegalName" or
                "Email" or
                "Phone" or
                "DateOfBirth" or
                "NationalityCountryCode" or
                "PreferredLanguageTag" or
                "Notes");
    }

    [Fact]
    public void Failed_outcome_does_not_change_the_profile()
    {
        GuestProfile profile = Create("Ada Guest", "ada@example.test", null);

        Result<GuestProfileUpdateOutcome> result = profile.UpdateWithOutcome(
            "Changed",
            null,
            "changed@example.test",
            null,
            profile.DateOfBirth,
            profile.NationalityCountryCode,
            profile.PreferredLanguageTag,
            profile.Notes,
            expectedVersion: profile.Version + 1,
            "user:operator-b",
            Guid.NewGuid(),
            Now.AddMinutes(1));

        Assert.True(result.IsFailure);
        Assert.Equal("Guests.VersionConflict", result.Error.Code);
        Assert.Equal("Ada Guest", profile.DisplayName);
        Assert.Equal("ada@example.test", profile.Email);
        Assert.Equal(1, profile.Version);
    }

    [Fact]
    public void Update_outcome_covers_every_correctable_profile_field()
    {
        GuestProfile profile = Create("Ada Guest", "ada@example.test", "+1 555 0100");

        Result<GuestProfileUpdateOutcome> result = profile.UpdateWithOutcome(
            "Ada Lovelace",
            "Augusta Ada King",
            "new@example.test",
            "+44 20 1234 5678",
            new DateOnly(1815, 12, 10),
            "GB",
            "en-GB",
            "Returning guest",
            profile.Version,
            "user:operator-b",
            Guid.NewGuid(),
            Now.AddMinutes(1));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [
                GuestProfileField.DisplayName,
                GuestProfileField.LegalName,
                GuestProfileField.Email,
                GuestProfileField.Phone,
                GuestProfileField.DateOfBirth,
                GuestProfileField.NationalityCountryCode,
                GuestProfileField.PreferredLanguageTag,
                GuestProfileField.Notes
            ],
            result.Value.ChangedFields);
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
