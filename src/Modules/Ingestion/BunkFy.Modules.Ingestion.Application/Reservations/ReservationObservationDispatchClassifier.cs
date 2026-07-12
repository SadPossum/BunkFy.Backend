namespace BunkFy.Modules.Ingestion.Application.Reservations;

using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Domain.Reservations;

internal static class ReservationObservationDispatchClassifier
{
    public static ReservationDispatchKind Classify(
        ReservationSourceLink link,
        NormalizedReservationObservation observation)
    {
        if (!link.ReservationId.HasValue)
        {
            return ReservationDispatchKind.Create;
        }

        if (observation.Kind == NormalizedReservationObservationKind.Cancel)
        {
            return ReservationDispatchKind.Cancel;
        }

        ReservationOperationalBaseline? baseline =
            ReservationOperationalBaseline.Deserialize(link.LastAppliedOperationalBaseline);
        if (baseline is null || baseline.Arrival != observation.Arrival || baseline.Departure != observation.Departure ||
            !baseline.InventoryUnitIds.Order().SequenceEqual(observation.InventoryUnitIds.Order()))
        {
            return ReservationDispatchKind.Amend;
        }

        return ReservationDispatchKind.ChangeGuestDetails;
    }
}
