namespace BunkFy.Modules.Reservations.Application.Handlers;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.GuestRecords;
using Gma.Framework.Messaging;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

[IntegrationEventHandler(ReservationsModuleMetadata.GuestCreatedHandlerName)]
internal sealed class GuestProfileCreatedProjectionHandler
    : IIntegrationEventHandler<GuestProfileCreatedIntegrationEvent>
{
    private readonly IReservationGuestProfileProjectionRepository profiles;
    private readonly IReservationGuestRecordLinkProcessRepository? processes;
    private readonly ReservationMutationCoordinator? mutations;
    private readonly ReservationInboxDomainEventDispatcher? domainEvents;
    private readonly ISystemClock? clock;
    private readonly IIdGenerator? ids;

    public GuestProfileCreatedProjectionHandler(
        IReservationGuestProfileProjectionRepository profiles,
        IReservationGuestRecordLinkProcessRepository processes,
        ReservationMutationCoordinator mutations,
        ReservationInboxDomainEventDispatcher domainEvents,
        ISystemClock clock,
        IIdGenerator ids)
    {
        this.profiles = profiles;
        this.processes = processes;
        this.mutations = mutations;
        this.domainEvents = domainEvents;
        this.clock = clock;
        this.ids = ids;
    }

    internal GuestProfileCreatedProjectionHandler(
        IReservationGuestProfileProjectionRepository profiles) =>
        this.profiles = profiles;

    public async Task HandleAsync(
        GuestProfileCreatedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        await this.profiles.ApplyAsync(
            new(integrationEvent.ScopeId, integrationEvent.GuestId, integrationEvent.OriginPropertyId, integrationEvent.Status, integrationEvent.GuestVersion),
            cancellationToken).ConfigureAwait(false);
        await this.profiles.ApplyRestrictionAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.OriginPropertyId,
                integrationEvent.GuestId,
                GuestProcessingRestrictionContract.CurrentVersion,
                Revision: 0,
                IsRestricted: false),
            cancellationToken).ConfigureAwait(false);

        if (!integrationEvent.CreationConfirmationId.HasValue)
        {
            return;
        }

        if (this.processes is null ||
            this.mutations is null ||
            this.domainEvents is null ||
            this.clock is null ||
            this.ids is null)
        {
            throw new InvalidOperationException(
                ReservationsApplicationErrors.GuestRecordLinkTaskInvalid.Code);
        }

        ReservationGuestRecordLinkProcess? candidate = await this.processes
            .GetByConfirmationAsync(
                integrationEvent.CreationConfirmationId.Value,
                cancellationToken)
            .ConfigureAwait(false);
        if (candidate is null)
        {
            return;
        }

        if (candidate.Id != integrationEvent.GuestId ||
            candidate.PropertyId != integrationEvent.OriginPropertyId)
        {
            throw new InvalidOperationException(
                ReservationsApplicationErrors.GuestRecordLinkProcessConflict.Code);
        }

        if (!await this.mutations.AcquireExistingAsync(
            candidate.ReservationId,
            cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                ReservationsApplicationErrors.GuestRecordLinkProcessNotFound.Code);
        }

        ReservationGuestRecordLinkProcess? process = await this.processes
            .GetByOperationAsync(candidate.Id, cancellationToken)
            .ConfigureAwait(false);
        if (process is null ||
            process.CreationConfirmationId !=
                integrationEvent.CreationConfirmationId.Value ||
            process.PropertyId != integrationEvent.OriginPropertyId)
        {
            throw new InvalidOperationException(
                ReservationsApplicationErrors.GuestRecordLinkProcessConflict.Code);
        }

        Result<bool> confirmed = process.ConfirmGuest(
            integrationEvent.CreationConfirmationId.Value,
            this.ids.NewId(),
            this.clock.UtcNow);
        if (confirmed.IsFailure)
        {
            throw new InvalidOperationException(confirmed.Error.Code);
        }

        await this.domainEvents.DispatchAsync(process, cancellationToken)
            .ConfigureAwait(false);
    }
}

[IntegrationEventHandler(ReservationsModuleMetadata.GuestUpdatedHandlerName)]
internal sealed class GuestProfileUpdatedProjectionHandler(IReservationGuestProfileProjectionRepository profiles)
    : IIntegrationEventHandler<GuestProfileUpdatedIntegrationEvent>
{
    public Task HandleAsync(GuestProfileUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        profiles.ApplyAsync(
            new(integrationEvent.ScopeId, integrationEvent.GuestId, null, integrationEvent.Status, integrationEvent.GuestVersion),
            cancellationToken);
}

[IntegrationEventHandler(ReservationsModuleMetadata.GuestArchivedHandlerName)]
internal sealed class GuestProfileArchivedProjectionHandler(IReservationGuestProfileProjectionRepository profiles)
    : IIntegrationEventHandler<GuestProfileArchivedIntegrationEvent>
{
    public Task HandleAsync(GuestProfileArchivedIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        profiles.ApplyAsync(
            new(integrationEvent.ScopeId, integrationEvent.GuestId, null, GuestStatus.Archived, integrationEvent.GuestVersion),
            cancellationToken);
}

[IntegrationEventHandler(ReservationsModuleMetadata.GuestAnonymisedHandlerName)]
internal sealed class GuestProfileAnonymisedProjectionHandler
    : IIntegrationEventHandler<GuestProfileAnonymisedIntegrationEvent>
{
    private readonly IReservationGuestProfileProjectionRepository profiles;
    private readonly IReservationGuestRecordLinkProcessRepository? processes;
    private readonly ISystemClock? clock;

    public GuestProfileAnonymisedProjectionHandler(
        IReservationGuestProfileProjectionRepository profiles,
        IReservationGuestRecordLinkProcessRepository processes,
        ISystemClock clock)
    {
        this.profiles = profiles;
        this.processes = processes;
        this.clock = clock;
    }

    internal GuestProfileAnonymisedProjectionHandler(
        IReservationGuestProfileProjectionRepository profiles) =>
        this.profiles = profiles;

    public async Task HandleAsync(
        GuestProfileAnonymisedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        if (this.processes is not null && this.clock is not null)
        {
            ReservationGuestRecordLinkProcess? process = await this.processes
                .GetByOperationAsync(
                    integrationEvent.GuestId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (process is not null)
            {
                Result<bool> terminated =
                    process.TerminateForGuestAnonymisation(this.clock.UtcNow);
                if (terminated.IsFailure)
                {
                    throw new InvalidOperationException(terminated.Error.Code);
                }
            }
        }

        await this.profiles.ApplyAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.GuestId,
                OriginPropertyId: null,
                GuestStatus.Archived,
                integrationEvent.GuestVersion),
            cancellationToken).ConfigureAwait(false);
    }
}

[IntegrationEventHandler(ReservationsModuleMetadata.GuestRestrictionChangedHandlerName)]
internal sealed class GuestProcessingRestrictionChangedProjectionHandler(
    IReservationGuestProfileProjectionRepository profiles)
    : IIntegrationEventHandler<GuestProcessingRestrictionChangedIntegrationEvent>
{
    public Task HandleAsync(
        GuestProcessingRestrictionChangedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken) =>
        profiles.ApplyRestrictionAsync(
            new(
                integrationEvent.ScopeId,
                integrationEvent.PropertyId,
                integrationEvent.GuestId,
                integrationEvent.ContractVersion,
                integrationEvent.ProjectionRevision,
                integrationEvent.IsRestricted),
            cancellationToken);
}
