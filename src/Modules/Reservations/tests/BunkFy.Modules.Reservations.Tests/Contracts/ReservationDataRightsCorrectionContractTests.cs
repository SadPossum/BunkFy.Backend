namespace BunkFy.Modules.Reservations.Tests.Contracts;

using BunkFy.Modules.Reservations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationDataRightsCorrectionContractTests
{
    [Fact]
    public void Applied_event_accepts_only_known_field_identifiers()
    {
        ReservationDataRightsCorrectionAppliedIntegrationEvent valid = CreateEvent(
            [ReservationDataRightsFieldKeys.PrimaryGuestName]);

        Assert.Equal(
            [ReservationDataRightsFieldKeys.PrimaryGuestName],
            valid.ChangedFields);
        Assert.Throws<ArgumentException>(() => CreateEvent(["corrected@example.test"]));
    }

    private static ReservationDataRightsCorrectionAppliedIntegrationEvent CreateEvent(
        IReadOnlyCollection<string> changedFields) => new(
        Guid.NewGuid(),
        "tenant-a",
        new DateTimeOffset(2026, 7, 25, 12, 0, 0, TimeSpan.Zero),
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        approvalRevision: 2,
        Guid.NewGuid(),
        previousVersion: 3,
        currentVersion: 4,
        previousDetailsRevision: 1,
        currentDetailsRevision: 2,
        changedFields,
        Guid.NewGuid());
}
