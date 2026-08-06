namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.DataGovernance;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Application.Policies;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal sealed class CreateReservationCommandHandler(
    IReservationRepository reservations,
    ReservationMutationCoordinator mutationCoordinator,
    IInventoryProjectionRepository inventoryProjection,
    IReservationCountryPolicyAdmission countryPolicy,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreateReservationCommand, ReservationMutationReceiptDto>
{
    public async Task<Result<ReservationMutationReceiptDto>> HandleAsync(
        CreateReservationCommand command,
        CancellationToken cancellationToken)
    {
        string? scopeId = scopeContext.ScopeId;
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeId))
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.TenantRequired);
        }

        CountryPolicyDecision policyDecision = await countryPolicy.EvaluateAsync(
            command.PropertyId,
            ReservationCountryPolicyAdmission.ReservationManagementPurpose,
            CountryPolicySurface.ApiWrite,
            ReservationCountryPolicyAdmission.AuthorizedOperatorProvenance,
            cancellationToken).ConfigureAwait(false);
        if (!policyDecision.IsAllowed)
        {
            return Result.Failure<ReservationMutationReceiptDto>(
                ReservationsApplicationErrors.CountryPolicyDenied(policyDecision.Reason));
        }

        ReservationSource? source = command.SourceKind switch
        {
            ReservationSourceKind.Direct => ReservationSource.Direct,
            ReservationSourceKind.External => ReservationSource.External,
            _ => null
        };
        if (!source.HasValue)
        {
            return Result.Failure<ReservationMutationReceiptDto>(
                ReservationsApplicationErrors.SourceInvalid);
        }

        ReservationCreationSnapshot creation = ReservationCreationSnapshot.Capture(
            command.PropertyId,
            command.Arrival,
            command.Departure,
            command.ExpectedArrivalTime,
            command.ExpectedDepartureTime,
            command.InventoryUnitIds,
            command.PrimaryGuestName,
            command.Email,
            command.Phone,
            command.GuestCount,
            source.Value,
            command.SourceSystem,
            command.SourceReference,
            command.Notes);
        Reservation? existing = await mutationCoordinator.AcquireCreationAsync(
            command.OperationId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesCreation(creation)
                ? Result.Success(existing.ToMutationReceipt())
                : Result.Failure<ReservationMutationReceiptDto>(
                    ReservationsApplicationErrors.CreationOperationConflict);
        }

        if (command.SourceKind == ReservationSourceKind.External &&
            !string.IsNullOrWhiteSpace(command.SourceSystem) &&
            !string.IsNullOrWhiteSpace(command.SourceReference) &&
            await reservations.ExternalSourceExistsAsync(
                command.SourceSystem.Trim().ToLowerInvariant(),
                command.SourceReference.Trim(),
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.ExternalSourceAlreadyExists);
        }

        InventoryUnitSelectionValidation unitValidation = await inventoryProjection
            .ValidateSelectionAsync(command.PropertyId, command.InventoryUnitIds, cancellationToken)
            .ConfigureAwait(false);
        if (unitValidation == InventoryUnitSelectionValidation.UnitNotFound)
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.InventoryUnitNotFound);
        }

        if (unitValidation == InventoryUnitSelectionValidation.PropertyMismatch)
        {
            return Result.Failure<ReservationMutationReceiptDto>(ReservationsApplicationErrors.InventoryUnitPropertyMismatch);
        }

        Result<Reservation> created = Reservation.Create(
            command.OperationId,
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
            source.Value,
            command.SourceSystem,
            command.SourceReference,
            command.Notes,
            idGenerator.NewId(),
            idGenerator.NewId(),
            ReservationDetailsChangeOrigin.Staff,
            initialDetailsActorId: command.ActorId,
            initialAdapterConnectionId: null,
            initialExternalOperationId: null,
            command.OperationId,
            clock.UtcNow,
            command.ExpectedArrivalTime,
            command.ExpectedDepartureTime);
        if (created.IsFailure)
        {
            return Result.Failure<ReservationMutationReceiptDto>(created.Error);
        }

        await reservations.AddUnderAcquiredOperationLockAsync(
            created.Value,
            cancellationToken).ConfigureAwait(false);
        return Result.Success(created.Value.ToMutationReceipt());
    }
}
