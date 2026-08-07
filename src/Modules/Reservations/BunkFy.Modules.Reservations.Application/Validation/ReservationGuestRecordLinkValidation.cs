namespace BunkFy.Modules.Reservations.Application.Validation;

internal static class ReservationGuestRecordLinkValidation
{
    public static IEnumerable<string> Coordinates(
        Guid operationId,
        Guid propertyId,
        Guid reservationId)
    {
        if (operationId == Guid.Empty)
        {
            yield return "OperationId is required.";
        }

        if (propertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (reservationId == Guid.Empty)
        {
            yield return "ReservationId is required.";
        }
    }
}
