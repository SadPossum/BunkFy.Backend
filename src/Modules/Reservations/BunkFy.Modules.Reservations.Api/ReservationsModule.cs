namespace BunkFy.Modules.Reservations.Api;

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
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Queries;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Persistence;

public sealed class ReservationsModule : IModule
{
    public string Name => ReservationsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(ReservationsProfiles.Default, "BunkFy.Modules.Reservations.Api");
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
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
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
                    request.Notes,
                    request.ExpectedArrivalTime,
                    request.ExpectedDepartureTime,
                    ResolveActor(httpContext, subjectResolver)),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .Produces<ReservationDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Create,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapGet("", async (
            Guid propertyId,
            ReservationStatus[]? status,
            string? search,
            ReservationListOrder? order,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListReservationsQuery(
                    propertyId,
                    status,
                    search,
                    order ?? ReservationListOrder.CreatedDescending,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .Produces<ReservationListResponse>(StatusCodes.Status200OK)
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
            .Produces<ReservationDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Read,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{reservationId:guid}/details-history", async (
            Guid propertyId,
            Guid reservationId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetReservationDetailsHistoryQuery(propertyId, reservationId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Read,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapPut("/{reservationId:guid}/guest-details", async (
            Guid propertyId,
            Guid reservationId,
            UpdateReservationGuestDetailsRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Gma.Framework.AccessControl.AccessSubject? subject = subjectResolver.ResolveSubject(httpContext);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(
                new UpdateReservationGuestDetailsCommand(
                    propertyId,
                    reservationId,
                    request.PrimaryGuestName,
                    request.Email,
                    request.Phone,
                    request.GuestCount,
                    request.Notes,
                    request.ExpectedDetailsRevision,
                    ReservationDetailsChangeOriginKind.Staff,
                    $"{Gma.Framework.AccessControl.AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}",
                    request.ExpectedArrivalTime,
                    request.ExpectedDepartureTime),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Manage,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapPut("/{reservationId:guid}/inventory", async (
            Guid propertyId,
            Guid reservationId,
            ReassignReservationInventoryRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string? actorId = ResolveActor(httpContext, subjectResolver);
            return actorId is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new ReassignReservationInventoryCommand(
                        propertyId,
                        reservationId,
                        request.AmendmentRequestId,
                        request.InventoryUnitIds,
                        request.ExpectedDetailsRevision,
                        actorId),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .Produces<ReservationDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Manage,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapPut("/{reservationId:guid}/guests", async (
            Guid propertyId,
            Guid reservationId,
            LinkReservationGuestRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string? actorId = ResolveActor(httpContext, subjectResolver);
            return actorId is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new LinkReservationGuestCommand(
                        propertyId,
                        reservationId,
                        request.GuestId,
                        request.Role,
                        request.ReplaceExistingRole,
                        request.ExpectedVersion,
                        actorId),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.ManageGuests,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{reservationId:guid}/cancel", async (
            Guid propertyId,
            Guid reservationId,
            CancelReservationRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CancelReservationCommand(
                    propertyId,
                    reservationId,
                    request.ExpectedVersion,
                    ResolveActor(httpContext, subjectResolver)),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .Produces<ReservationDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.Cancel,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{reservationId:guid}/check-in", async (
            Guid propertyId,
            Guid reservationId,
            StayLifecycleRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string? actorId = ResolveActor(httpContext, subjectResolver);
            return actorId is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new CheckInReservationCommand(
                        propertyId,
                        reservationId,
                        request.BusinessDate,
                        request.ExpectedVersion,
                        actorId),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.CheckIn,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{reservationId:guid}/no-show", async (
            Guid propertyId,
            Guid reservationId,
            StayLifecycleRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string? actorId = ResolveActor(httpContext, subjectResolver);
            return actorId is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new MarkReservationNoShowCommand(
                        propertyId,
                        reservationId,
                        request.BusinessDate,
                        request.ExpectedVersion,
                        actorId),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.NoShow,
                ReservationsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{reservationId:guid}/check-out", async (
            Guid propertyId,
            Guid reservationId,
            StayLifecycleRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string? actorId = ResolveActor(httpContext, subjectResolver);
            return actorId is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new CheckOutReservationCommand(
                        propertyId,
                        reservationId,
                        request.BusinessDate,
                        request.ExpectedVersion,
                        actorId),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                ReservationsAdminPermissionCodes.CheckOut,
                ReservationsPropertyAccessScopeResolver.ResolverName);
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

    public sealed record StayLifecycleRequest(DateOnly BusinessDate, long ExpectedVersion);

    public sealed record LinkReservationGuestRequest(
        Guid GuestId,
        ReservationGuestRoleKind Role,
        bool ReplaceExistingRole,
        long ExpectedVersion);

    public sealed record UpdateReservationGuestDetailsRequest(
        string PrimaryGuestName,
        string? Email,
        string? Phone,
        int GuestCount,
        string? Notes,
        TimeOnly? ExpectedArrivalTime,
        TimeOnly? ExpectedDepartureTime,
        long ExpectedDetailsRevision);

    public sealed record ReassignReservationInventoryRequest(
        Guid AmendmentRequestId,
        IReadOnlyCollection<Guid> InventoryUnitIds,
        long ExpectedDetailsRevision);

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = CreateErrorStatusCodes(
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

    private static ApiErrorStatusCodeMap CreateErrorStatusCodes(params ApiErrorStatusCode[] entries) =>
        ApiErrorStatusCodeMap.Create(entries.Concat(
            ReservationsApplicationErrors.CountryPolicyDenials.Select(error =>
                new ApiErrorStatusCode(error.Code, StatusCodes.Status409Conflict))).ToArray());

    private static string? ResolveActor(HttpContext context, IAccessHttpSubjectResolver subjectResolver)
    {
        Gma.Framework.AccessControl.AccessSubject? subject = subjectResolver.ResolveSubject(context);
        return subject is null
            ? null
            : $"{Gma.Framework.AccessControl.AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}";
    }
}
