namespace BunkFy.Modules.Ingestion.Application.Reservations;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Results;

internal sealed class ReservationAdapterIngressAdmissionPolicy : IAdapterIngressAdmissionPolicy
{
    public Result Validate(IReadOnlyCollection<AdapterIngressObservationRequest> records)
    {
        if (records is null || records.Count == 0)
        {
            return Invalid();
        }

        foreach (AdapterIngressObservationRequest record in records)
        {
            if (record is null ||
                !string.Equals(
                    record.RecordType,
                    ReservationObservationJsonNormalizer.RecordType,
                    StringComparison.Ordinal) ||
                !string.Equals(record.ContentType, "application/json", StringComparison.Ordinal) ||
                !string.Equals(
                    record.ContentSha256,
                    AdapterPayloadHash.ComputeSha256(record.Payload),
                    StringComparison.Ordinal))
            {
                return Invalid();
            }

            Result<NormalizedReservationObservation> normalized =
                ReservationObservationJsonNormalizer.Normalize(record.Payload);
            if (normalized.IsFailure || !IsApproved(normalized.Value))
            {
                return Invalid();
            }
        }

        return Result.Success();
    }

    private static bool IsApproved(NormalizedReservationObservation observation)
    {
        if (observation.Kind == NormalizedReservationObservationKind.Cancel)
        {
            return observation.Arrival is null &&
                   observation.Departure is null &&
                   observation.InventoryUnitIds.Count == 0 &&
                   observation.PrimaryGuestName is null &&
                   observation.Email is null &&
                   observation.Phone is null &&
                   observation.GuestCount is null &&
                   observation.Notes is null &&
                   observation.ExpectedArrivalTime is null &&
                   observation.ExpectedDepartureTime is null;
        }

        return observation.Kind == NormalizedReservationObservationKind.Upsert &&
               observation.InventoryUnitIds.Count <= ReservationsContractLimits.MaximumRequestedUnits &&
               observation.PrimaryGuestName is { Length: <= ReservationsContractLimits.PrimaryGuestNameMaxLength } &&
               (observation.Email is null ||
                observation.Email.Length <= ReservationsContractLimits.EmailMaxLength) &&
               (observation.Phone is null ||
                observation.Phone.Length <= ReservationsContractLimits.PhoneMaxLength) &&
               observation.Notes is null;
    }

    private static Result Invalid() => Result.Failure(IngestionApplicationErrors.IngressSubmissionInvalid);
}
