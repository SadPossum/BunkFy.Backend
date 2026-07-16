namespace BunkFy.Modules.Ingestion.Tests.Application;

using System.Text;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Reservations;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationObservationJsonNormalizerTests
{
    [Fact]
    public void Canonical_reservation_document_is_normalized()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            """
            {"operation":"upsert","sourceSequence":7,"arrival":"2026-08-01","departure":"2026-08-03","expectedArrivalTime":"15:30:00","expectedDepartureTime":"10:45:00","inventoryUnitIds":["20000000-0000-0000-0000-000000000001"],"primaryGuestName":" Ada Guest ","email":null,"phone":null,"guestCount":1,"notes":null}
            """);

        Gma.Framework.Results.Result<NormalizedReservationObservation> result =
            ReservationObservationJsonNormalizer.Normalize(payload);

        Assert.True(result.IsSuccess);
        Assert.Equal(NormalizedReservationObservationKind.Upsert, result.Value.Kind);
        Assert.Equal(7, result.Value.SourceSequence);
        Assert.Equal("Ada Guest", result.Value.PrimaryGuestName);
        Assert.Equal(new TimeOnly(15, 30), result.Value.ExpectedArrivalTime);
        Assert.Equal(new TimeOnly(10, 45), result.Value.ExpectedDepartureTime);
    }

    [Fact]
    public void Expected_times_with_seconds_are_rejected()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            "{\"operation\":\"upsert\",\"arrival\":\"2026-08-01\",\"departure\":\"2026-08-03\",\"expectedArrivalTime\":\"15:30:01\",\"inventoryUnitIds\":[\"20000000-0000-0000-0000-000000000001\"],\"primaryGuestName\":\"Ada Guest\",\"guestCount\":1}");

        Assert.True(ReservationObservationJsonNormalizer.Normalize(payload).IsFailure);
    }

    [Fact]
    public void Unknown_fields_are_rejected_instead_of_silently_ignored()
    {
        byte[] payload = Encoding.UTF8.GetBytes("{\"operation\":\"cancel\",\"unexpected\":true}");

        Assert.True(ReservationObservationJsonNormalizer.Normalize(payload).IsFailure);
    }
}
