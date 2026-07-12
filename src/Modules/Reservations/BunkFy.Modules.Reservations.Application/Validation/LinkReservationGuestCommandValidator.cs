namespace BunkFy.Modules.Reservations.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;

internal sealed class LinkReservationGuestCommandValidator : ICommandValidator<LinkReservationGuestCommand>
{
    public IEnumerable<string> Validate(LinkReservationGuestCommand command)
    {
        if (command.PropertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (command.ReservationId == Guid.Empty)
        {
            yield return "ReservationId is required.";
        }

        if (command.GuestId == Guid.Empty)
        {
            yield return "GuestId is required.";
        }

        if (command.Role is not ReservationGuestRoleKind.Primary)
        {
            yield return "Role is invalid.";
        }

        if (command.ExpectedVersion <= 0)
        {
            yield return "ExpectedVersion must be greater than zero.";
        }

        if (string.IsNullOrWhiteSpace(command.ActorId) || command.ActorId.Trim().Length > ReservationsContractLimits.ActorIdMaxLength)
        {
            yield return "ActorId is required and must be within the supported limit.";
        }
    }
}
