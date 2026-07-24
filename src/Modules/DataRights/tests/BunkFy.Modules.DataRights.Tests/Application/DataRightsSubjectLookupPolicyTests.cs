namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Validation;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsSubjectLookupPolicyTests
{
    [Fact]
    public void Exactly_one_guest_id_email_or_phone_is_required()
    {
        DataRightsSubjectLookup[] invalidLookups =
        [
            new(null, null, null, "Guest Name", new DateOnly(1990, 1, 1)),
            new(Guid.Empty, null, null, null, null),
            new(Guid.NewGuid(), "guest@example.test", null, null, null),
            new(null, "guest@example.test", "+44 20 1234 5678", null, null)
        ];

        foreach (DataRightsSubjectLookup lookup in invalidLookups)
        {
            Assert.Contains(
                "Exactly one non-empty guest id, email, or phone is required.",
                DataRightsSubjectLookupPolicy.Validate(lookup));
            Assert.True(DataRightsSubjectLookupPolicy.Normalize(lookup).IsFailure);
        }
    }

    [Fact]
    public void Valid_lookup_is_trimmed_and_email_is_normalized()
    {
        DataRightsSubjectLookup lookup = new(
            null,
            "  Guest@Example.Test  ",
            null,
            "  Guest Name  ",
            new DateOnly(1990, 1, 1));

        Result<DataRightsSubjectLookup> result = DataRightsSubjectLookupPolicy.Normalize(lookup);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.RecordId);
        Assert.Equal("guest@example.test", result.Value.Email);
        Assert.Null(result.Value.Phone);
        Assert.Equal("Guest Name", result.Value.Name);
        Assert.Equal(new DateOnly(1990, 1, 1), result.Value.DateOfBirth);
    }

    [Fact]
    public void Invalid_or_oversized_optional_coordinates_are_rejected()
    {
        DataRightsSubjectLookup[] invalidLookups =
        [
            new(null, "not-an-email", null, null, null),
            new(null, null, new string('1', 65), null, null),
            new(
                null,
                "guest@example.test",
                null,
                new string('n', DataRightsSubjectDiscoveryLimits.DisplayNameMaxLength + 1),
                null)
        ];

        Assert.All(invalidLookups, lookup =>
            Assert.True(DataRightsSubjectLookupPolicy.Normalize(lookup).IsFailure));
    }
}
