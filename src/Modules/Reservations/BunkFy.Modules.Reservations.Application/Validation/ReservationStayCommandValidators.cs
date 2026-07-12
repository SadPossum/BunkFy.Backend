namespace BunkFy.Modules.Reservations.Application.Validation;

using Gma.Framework.Cqrs;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Contracts;

internal abstract class ReservationStayCommandValidator
{
    protected static IEnumerable<string> ValidateFields(
        Guid propertyId,
        Guid reservationId,
        long expectedVersion,
        string actorId)
    {
        if (propertyId == Guid.Empty)
        {
            yield return "PropertyId is required.";
        }

        if (reservationId == Guid.Empty)
        {
            yield return "ReservationId is required.";
        }

        if (expectedVersion <= 0)
        {
            yield return "ExpectedVersion must be greater than zero.";
        }

        string normalizedActor = actorId?.Trim() ?? string.Empty;
        if (normalizedActor.Length is 0 or > ReservationsContractLimits.ActorIdMaxLength)
        {
            yield return "ActorId is required and must be within the supported limit.";
        }
    }
}

internal sealed class CheckInReservationCommandValidator : ReservationStayCommandValidator,
    ICommandValidator<CheckInReservationCommand>
{
    public IEnumerable<string> Validate(CheckInReservationCommand command) => ValidateFields(
        command.PropertyId, command.ReservationId, command.ExpectedVersion, command.ActorId);
}

internal sealed class MarkReservationNoShowCommandValidator : ReservationStayCommandValidator,
    ICommandValidator<MarkReservationNoShowCommand>
{
    public IEnumerable<string> Validate(MarkReservationNoShowCommand command) => ValidateFields(
        command.PropertyId, command.ReservationId, command.ExpectedVersion, command.ActorId);
}

internal sealed class CheckOutReservationCommandValidator : ReservationStayCommandValidator,
    ICommandValidator<CheckOutReservationCommand>
{
    public IEnumerable<string> Validate(CheckOutReservationCommand command) => ValidateFields(
        command.PropertyId, command.ReservationId, command.ExpectedVersion, command.ActorId);
}
