namespace BunkFy.Modules.Reservations.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class ListReservationsQueryValidator : IQueryValidator<ListReservationsQuery>
{
    public IEnumerable<string> Validate(ListReservationsQuery query)
    {
        if (query.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (query.Status.HasValue &&
            (query.Status.Value == ReservationStatus.Unknown || !Enum.IsDefined(query.Status.Value)))
        {
            yield return "Status is invalid.";
        }
    }
}
