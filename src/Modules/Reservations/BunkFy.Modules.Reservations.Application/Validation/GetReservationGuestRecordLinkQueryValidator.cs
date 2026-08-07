namespace BunkFy.Modules.Reservations.Application.Validation;

using BunkFy.Modules.Reservations.Application.Queries;
using Gma.Framework.Cqrs;

internal sealed class GetReservationGuestRecordLinkQueryValidator
    : IQueryValidator<GetReservationGuestRecordLinkQuery>
{
    public IEnumerable<string> Validate(
        GetReservationGuestRecordLinkQuery query) =>
        ReservationGuestRecordLinkValidation.Coordinates(
            query.OperationId,
            query.PropertyId,
            query.ReservationId);
}
