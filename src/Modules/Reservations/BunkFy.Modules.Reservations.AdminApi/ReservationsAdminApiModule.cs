namespace BunkFy.Modules.Reservations.AdminApi;

using System.Security.Claims;
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
using BunkFy.Modules.Reservations.Admin.Contracts;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Persistence;

public sealed class ReservationsAdminApiModule : IAdminApiModule
{
    public string Name => ReservationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(ReservationsProfiles.Default, "BunkFy.Modules.Reservations.AdminApi");
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
                        status.HasValue ? [status.Value] : null,
                        null,
                        ReservationListOrder.CreatedDescending,
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

        group.MapGet("/{reservationId:guid}/details-history", async (
            Guid propertyId,
            Guid reservationId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(ReservationsAdminOperationNames.History, ReservationsAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new GetReservationDetailsHistoryQuery(propertyId, reservationId),
                    token),
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
                        request.Notes,
                        request.ExpectedArrivalTime,
                        request.ExpectedDepartureTime),
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

        group.MapPut("/{reservationId:guid}/guests", async (
            Guid propertyId,
            Guid reservationId,
            LinkReservationGuestRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(ReservationsAdminOperationNames.LinkGuest, ReservationsAdminPermissions.ManageGuests),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new LinkReservationGuestCommand(
                        propertyId,
                        reservationId,
                        request.GuestId,
                        request.Role,
                        request.ReplaceExistingRole,
                        request.ExpectedVersion,
                        Actor(httpContext)),
                    token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPut("/{reservationId:guid}/inventory", async (
            Guid propertyId,
            Guid reservationId,
            ReassignReservationInventoryRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(
                    ReservationsAdminOperationNames.ReassignInventory,
                    ReservationsAdminPermissions.Manage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new ReassignReservationInventoryCommand(
                        propertyId,
                        reservationId,
                        request.AmendmentRequestId,
                        request.InventoryUnitIds,
                        request.ExpectedDetailsRevision,
                        Actor(httpContext)),
                    token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPost("/{reservationId:guid}/check-in", async (
            Guid propertyId,
            Guid reservationId,
            StayLifecycleRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(ReservationsAdminOperationNames.CheckIn, ReservationsAdminPermissions.CheckIn),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new CheckInReservationCommand(
                        propertyId, reservationId, request.BusinessDate, request.ExpectedVersion, Actor(httpContext)), token)
                    : Task.FromResult(Gma.Framework.Results.Result.Failure<ReservationDto>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPost("/{reservationId:guid}/no-show", async (
            Guid propertyId,
            Guid reservationId,
            StayLifecycleRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(ReservationsAdminOperationNames.NoShow, ReservationsAdminPermissions.NoShow),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new MarkReservationNoShowCommand(
                        propertyId, reservationId, request.BusinessDate, request.ExpectedVersion, Actor(httpContext)), token)
                    : Task.FromResult(Gma.Framework.Results.Result.Failure<ReservationDto>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPost("/{reservationId:guid}/check-out", async (
            Guid propertyId,
            Guid reservationId,
            StayLifecycleRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(ReservationsAdminOperationNames.CheckOut, ReservationsAdminPermissions.CheckOut),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new CheckOutReservationCommand(
                        propertyId, reservationId, request.BusinessDate, request.ExpectedVersion, Actor(httpContext)), token)
                    : Task.FromResult(Gma.Framework.Results.Result.Failure<ReservationDto>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    public sealed record CreateReservationRequest(
        DateOnly Arrival,
        DateOnly Departure,
        TimeOnly? ExpectedArrivalTime,
        TimeOnly? ExpectedDepartureTime,
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

    public sealed record StayLifecycleRequest(DateOnly BusinessDate, long ExpectedVersion, bool Confirmed);

    public sealed record LinkReservationGuestRequest(
        Guid GuestId,
        ReservationGuestRoleKind Role,
        bool ReplaceExistingRole,
        long ExpectedVersion);

    public sealed record ReassignReservationInventoryRequest(
        Guid AmendmentRequestId,
        IReadOnlyCollection<Guid> InventoryUnitIds,
        long ExpectedDetailsRevision);

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(ReservationsApplicationErrors.ReservationNotFound.Code, StatusCodes.Status404NotFound),
        new(ReservationsApplicationErrors.ExternalSourceAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.InventoryUnitNotFound.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.InventoryUnitPropertyMismatch.Code, StatusCodes.Status400BadRequest),
        new(ReservationsApplicationErrors.ExpectedStayTimeInvalid.Code, StatusCodes.Status400BadRequest),
        new(ReservationsApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.DetailsRevisionConflict.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.DetailsChangeProvenanceInvalid.Code, StatusCodes.Status400BadRequest),
        new(ReservationsApplicationErrors.AllocationAmendmentInProgress.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.AllocationAmendmentInvalid.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.StayBusinessDateInvalid.Code, StatusCodes.Status400BadRequest),
        new(ReservationsApplicationErrors.StayProvenanceInvalid.Code, StatusCodes.Status400BadRequest),
        new(ReservationsApplicationErrors.GuestNotLinkable.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.ReservationGuestLinkInvalid.Code, StatusCodes.Status400BadRequest),
        new(ReservationsApplicationErrors.ReservationGuestRoleOccupied.Code, StatusCodes.Status409Conflict),
        new(ReservationsApplicationErrors.InvalidTransition.Code, StatusCodes.Status409Conflict));

    private static string Actor(HttpContext context)
    {
        string identity = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.Identity?.Name
            ?? $"authenticated:{context.User.Identity?.AuthenticationType ?? "unknown"}";
        return $"admin-api:{identity}";
    }
}
