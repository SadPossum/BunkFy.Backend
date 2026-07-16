namespace BunkFy.Modules.Reservations.Tests;

using BunkFy.Modules.Reservations.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ExternalReservationContractTests
{
    [Fact]
    public void Create_allows_omitted_optional_contact_fields_and_normalizes_source_system()
    {
        ExternalReservationCreateRequestedIntegrationEvent request = new(
            Guid.NewGuid(),
            "tenant-a",
            DateTimeOffset.UtcNow,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Fake.HTTP  ",
            " booking-42 ",
            new DateOnly(2026, 8, 1),
            new DateOnly(2026, 8, 3),
            [Guid.NewGuid()],
            "Ada Guest",
            email: null,
            phone: null,
            guestCount: 1,
            notes: null,
            expectedArrivalTime: new TimeOnly(15, 30),
            expectedDepartureTime: new TimeOnly(10, 45));

        Assert.Equal("fake.http", request.SourceSystem);
        Assert.Equal("booking-42", request.SourceReference);
        Assert.Null(request.Email);
        Assert.Null(request.Phone);
        Assert.Null(request.Notes);
        Assert.Equal(2, request.Version);
        Assert.Equal(new TimeOnly(15, 30), request.ExpectedArrivalTime);
        Assert.Equal(new TimeOnly(10, 45), request.ExpectedDepartureTime);
    }

    [Fact]
    public void Amendment_carries_the_complete_candidate_and_defensively_copies_units()
    {
        Guid unitId = Guid.NewGuid();
        Guid[] units = [unitId];
        ExternalReservationAmendmentRequestedIntegrationEvent request = new(
            Guid.NewGuid(), "tenant-a", DateTimeOffset.UtcNow, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "fake.http", "booking-42", 3,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 4), units,
            "Ada Updated", null, null, 2, "Window bed", new TimeOnly(16, 0), new TimeOnly(11, 0));

        units[0] = Guid.NewGuid();

        Assert.Equal(unitId, Assert.Single(request.InventoryUnitIds));
        Assert.Equal(3, request.ExpectedDetailsRevision);
        Assert.Equal("Ada Updated", request.PrimaryGuestName);
        Assert.Equal(new TimeOnly(16, 0), request.ExpectedArrivalTime);
        Assert.Equal(new TimeOnly(11, 0), request.ExpectedDepartureTime);
    }
}
