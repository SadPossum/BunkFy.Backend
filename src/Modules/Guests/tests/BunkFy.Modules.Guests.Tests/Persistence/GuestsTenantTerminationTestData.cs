namespace BunkFy.Modules.Guests.Tests.Persistence;

using BunkFy.Modules.Guests.Domain.Aggregates;

internal static class GuestsTenantTerminationTestData
{
    public const string TenantId =
        "10000000-0000-0000-0000-000000000001";

    public static readonly DateTimeOffset Now =
        new(2026, 7, 31, 18, 0, 0, TimeSpan.Zero);

    public static GuestProfile CreateProfile(
        Guid? propertyId = null,
        string displayName = "Maya Chen",
        Guid? guestId = null,
        Guid? creationConfirmationId = null) =>
        GuestProfile.Create(
            guestId ?? Guid.NewGuid(),
            TenantId,
            propertyId ?? Guid.NewGuid(),
            displayName,
            "Maya Q. Chen",
            "maya@example.test",
            "+44 20 1234 5678",
            new DateOnly(1990, 2, 3),
            "GB",
            "en-GB",
            "Prefers a lower bunk.",
            "user:owner",
            Guid.NewGuid(),
            Now.AddDays(-1),
            creationConfirmationId).Value;
}
