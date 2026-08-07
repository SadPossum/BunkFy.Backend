namespace BunkFy.Modules.Reservations.Application.Validation;

using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Cqrs;

internal sealed class PrepareReservationGuestRecordLinkCommandValidator
    : ICommandValidator<PrepareReservationGuestRecordLinkCommand>
{
    public IEnumerable<string> Validate(
        PrepareReservationGuestRecordLinkCommand command)
    {
        foreach (string error in ReservationGuestRecordLinkValidation.Coordinates(
            command.OperationId,
            command.PropertyId,
            command.ReservationId))
        {
            yield return error;
        }

        if (command.ExpectedReservationVersion <= 0)
        {
            yield return "ExpectedReservationVersion must be greater than zero.";
        }

        string actor = command.ActorId?.Trim() ?? string.Empty;
        if (actor.Length is 0 or > ReservationsContractLimits.ActorIdMaxLength)
        {
            yield return "ActorId is required and must be within the supported limit.";
        }
    }
}
