namespace Reservations.AdminApi;

using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Reservations.Admin.Contracts;
using Reservations.Application;
using Reservations.Application.Commands;
using Reservations.Application.Queries;
using Reservations.Contracts;
using Reservations.Persistence;

public sealed class ReservationsAdminApiModule : IAdminApiModule
{
    public string Name => ReservationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(ReservationsProfiles.Default, "Reservations.AdminApi");
        builder.Services.AddReservationsApplication();
        builder.AddReservationsPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/admin/reservations/properties/{propertyId:guid}")
            .WithModuleName(this.Name)
            .WithTags("Reservations Admin")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid propertyId,
            ReservationStatus? status,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(ReservationsAdminOperationNames.List, ReservationsAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListReservationsQuery(
                        propertyId,
                        status,
                        page ?? PageRequest.DefaultPage,
                        pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapGet("/{reservationId:guid}", async (
            Guid propertyId,
            Guid reservationId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(ReservationsAdminOperationNames.Get, ReservationsAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetReservationQuery(propertyId, reservationId), token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPost("", async (
            Guid propertyId,
            CreateReservationRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(ReservationsAdminOperationNames.Create, ReservationsAdminPermissions.Create),
                requireTenant: true,
                token => dispatcher.SendAsync(
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
                    token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPost("/{reservationId:guid}/cancel", async (
            Guid propertyId,
            Guid reservationId,
            CancelReservationRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(ReservationsAdminOperationNames.Cancel, ReservationsAdminPermissions.Cancel),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new CancelReservationCommand(propertyId, reservationId, request.ExpectedVersion),
                    token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
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
