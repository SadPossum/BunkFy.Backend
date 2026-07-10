namespace Reservations.Api;

using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Reservations.Application;
using Reservations.Application.Commands;
using Reservations.Application.Queries;
using Reservations.Contracts;
using Reservations.Persistence;

public sealed class ReservationsModule : IModule
{
    public string Name => ReservationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(ReservationsProfiles.Default, "Reservations.Api");
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAccessHttpScopeResolver, ReservationsPropertyAccessScopeResolver>());
        builder.Services.AddReservationsApplication();
        builder.AddReservationsPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/reservations/properties/{propertyId:guid}")
            .WithModuleName(this.Name)
            .WithTags("Reservations")
            .RequireAuthorization();

        group.MapPost("", async (
            Guid propertyId,
            CreateReservationRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CreateReservationCommand(
                    propertyId,
                    request.Arrival,
                    request.Departure,
                    request.InventoryUnitIds,
                    request.PrimaryGuestName,
                    request.Email,
                    request.Phone,
                    request.GuestCount,
                    request.SourceKind,
                    request.SourceSystem,
                    request.SourceReference,
                    request.Notes),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Create,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapGet("", async (
            Guid propertyId,
            ReservationStatus? status,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListReservationsQuery(
                    propertyId,
                    status,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Read,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{reservationId:guid}", async (
            Guid propertyId,
            Guid reservationId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetReservationQuery(propertyId, reservationId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Read,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{reservationId:guid}/cancel", async (
            Guid propertyId,
            Guid reservationId,
            CancelReservationRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CancelReservationCommand(propertyId, reservationId, request.ExpectedVersion),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Cancel,
                ReservationsPropertyAccessScopeResolver.ResolverName);
    }

    public sealed record CreateReservationRequest(
        DateOnly Arrival,
        DateOnly Departure,
        IReadOnlyCollection<Guid> InventoryUnitIds,
        string PrimaryGuestName,
        string? Email,
        string? Phone,
        int GuestCount,
        ReservationSourceKind SourceKind,
        string? SourceSystem,
        string? SourceReference,
        string? Notes);

    public sealed record CancelReservationRequest(long ExpectedVersion);

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(ReservationsApplicationErrors.ReservationNotFound.Code, StatusCodes.Status404NotFound),
        new(ReservationsApplicationErrors.ExternalSourceAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.InventoryUnitNotFound.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.InventoryUnitPropertyMismatch.Code, StatusCodes.Status400BadRequest),
        new(ReservationsApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.InvalidTransition.Code, StatusCodes.Status409Conflict));
}
