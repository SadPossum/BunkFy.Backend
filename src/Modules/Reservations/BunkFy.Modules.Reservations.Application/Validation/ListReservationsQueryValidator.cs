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

        if (query.Statuses?.Any(status => status == ReservationStatus.Unknown || !Enum.IsDefined(status)) == true)
        {
            yield return "Statuses contain an invalid value.";
        }

        if (query.Statuses?.Count > Enum.GetValues<ReservationStatus>().Length - 1)
        {
            yield return "Too many statuses were requested.";
        }

        if (query.Search?.Trim().Length > ReservationsContractLimits.SearchMaxLength)
        {
            yield return "Search exceeds the supported limit.";
        }

        if (query.Order == ReservationListOrder.Unknown || !Enum.IsDefined(query.Order))
        {
            yield return "Order is invalid.";
        }
    }
}
