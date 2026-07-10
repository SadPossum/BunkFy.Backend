namespace Reservations.Application.Handlers;

using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Reservations.Application.Commands;
using Reservations.Application.Ports;
using Reservations.Contracts;
using Reservations.Domain.Aggregates;

internal sealed class CreateReservationCommandHandler(
    IReservationRepository reservations,
    IInventoryProjectionRepository inventoryProjection,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreateReservationCommand, ReservationDto>
{
    public async Task<Result<ReservationDto>> HandleAsync(
        CreateReservationCommand command,
        CancellationToken cancellationToken)
    {
        string? scopeId = scopeContext.ScopeId;
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.TenantRequired);
        }

        if (command.SourceKind == ReservationSourceKind.External &&
            !string.IsNullOrWhiteSpace(command.SourceSystem) &&
            !string.IsNullOrWhiteSpace(command.SourceReference) &&
            await reservations.ExternalSourceExistsAsync(
                command.SourceSystem.Trim().ToLowerInvariant(),
                command.SourceReference.Trim(),
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.ExternalSourceAlreadyExists);
        }

        InventoryUnitSelectionValidation unitValidation = await inventoryProjection
            .ValidateSelectionAsync(command.PropertyId, command.InventoryUnitIds, cancellationToken)
            .ConfigureAwait(false);
        if (unitValidation == InventoryUnitSelectionValidation.UnitNotFound)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.InventoryUnitNotFound);
        }

        if (unitValidation == InventoryUnitSelectionValidation.PropertyMismatch)
        {
            return Result.Failure<ReservationDto>(ReservationsApplicationErrors.InventoryUnitPropertyMismatch);
        }

        Result<Reservation> created = Reservation.Create(
            idGenerator.NewId(),
            scopeId,
            command.PropertyId,
            idGenerator.NewId(),
            command.Arrival,
            command.Departure,
            command.InventoryUnitIds,
            command.PrimaryGuestName,
            command.Email,
            command.Phone,
            command.GuestCount,
            command.SourceKind == ReservationSourceKind.Direct ? ReservationSource.Direct : ReservationSource.External,
            command.SourceSystem,
            command.SourceReference,
            command.Notes,
            idGenerator.NewId(),
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<ReservationDto>(created.Error);
        }

        await reservations.AddAsync(created.Value, cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToDto());
    }
}
