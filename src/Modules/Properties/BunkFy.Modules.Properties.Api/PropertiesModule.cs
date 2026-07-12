namespace BunkFy.Modules.Properties.Api;

using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Queries;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;

public sealed class PropertiesModule : IModule
{
    public string Name => PropertiesModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(PropertiesProfiles.Default, "BunkFy.Modules.Properties.Api");
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAccessHttpScopeResolver, PropertyAccessScopeResolver>());
        builder.Services.AddPropertiesApplication();
        builder.AddPropertiesPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder properties = endpoints.MapGroup("/api/properties")
            .WithModuleName(this.Name)
            .WithTags("Properties")
            .RequireAuthorization();

        properties.MapGet("/", async (
            int? page,
            int? pageSize,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            AccessSubject? subject = subjectResolver.ResolveSubject(httpContext);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            Result<PropertyListResponse> result = await dispatcher.QueryAsync(
                new ListVisiblePropertiesQuery(
                    subject,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false);

            return result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant();

        properties.MapGet("/{propertyId:guid}", async (
            Guid propertyId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new GetPropertyQuery(propertyId), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.Read, PropertyAccessScopeResolver.ResolverName);

        properties.MapPost("/", async (
            PropertyCreateRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CreatePropertyCommand(request.Name, request.Code, request.TimeZoneId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireTenantPermission(PropertiesAdminPermissionCodes.PropertiesManage);

        properties.MapPut("/{propertyId:guid}", async (
            Guid propertyId,
            PropertyUpdateRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new UpdatePropertyCommand(propertyId, request.Name, request.Code, request.TimeZoneId, request.ExpectedVersion),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.PropertiesManage, PropertyAccessScopeResolver.ResolverName);

        properties.MapPost("/{propertyId:guid}/retire", async (
            Guid propertyId,
            RetirePropertyRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<Unit> result = request.Confirmed
                ? await dispatcher.SendAsync(
                    new RetirePropertyCommand(propertyId, request.ExpectedVersion),
                    cancellationToken).ConfigureAwait(false)
                : Result.Failure<Unit>(new("Properties.ConfirmationRequired", "Confirmation is required."));

            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.PropertiesManage, PropertyAccessScopeResolver.ResolverName);

        properties.MapGet("/{propertyId:guid}/rooms", async (
            Guid propertyId,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListRoomsQuery(propertyId, page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.Read, PropertyAccessScopeResolver.ResolverName);

        properties.MapPost("/{propertyId:guid}/rooms", async (
            Guid propertyId,
            RoomCreateRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CreateRoomCommand(
                    propertyId,
                    request.ExpectedPropertyVersion,
                    request.Name,
                    request.BuildingLabel,
                    request.FloorLabel),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.RoomsManage, PropertyAccessScopeResolver.ResolverName);

        properties.MapGet("/{propertyId:guid}/rooms/{roomId:guid}", async (
            Guid propertyId,
            Guid roomId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new GetRoomQuery(propertyId, roomId), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.Read, PropertyAccessScopeResolver.ResolverName);

        properties.MapPut("/{propertyId:guid}/rooms/{roomId:guid}", async (
            Guid propertyId,
            Guid roomId,
            RoomUpdateRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new UpdateRoomCommand(
                    propertyId,
                    roomId,
                    request.ExpectedVersion,
                    request.Name,
                    request.BuildingLabel,
                    request.FloorLabel),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.RoomsManage, PropertyAccessScopeResolver.ResolverName);

        properties.MapPost("/{propertyId:guid}/rooms/{roomId:guid}/retire", async (
            Guid propertyId,
            Guid roomId,
            RetireRoomRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<Unit> result = request.Confirmed
                ? await dispatcher.SendAsync(
                    new RetireRoomCommand(propertyId, roomId, request.ExpectedVersion, request.CascadeBeds),
                    cancellationToken).ConfigureAwait(false)
                : Result.Failure<Unit>(new("Properties.ConfirmationRequired", "Confirmation is required."));

            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.RoomsManage, PropertyAccessScopeResolver.ResolverName);

        properties.MapGet("/{propertyId:guid}/rooms/{roomId:guid}/beds", async (
            Guid propertyId,
            Guid roomId,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListBedsQuery(propertyId, roomId, page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.Read, PropertyAccessScopeResolver.ResolverName);

        properties.MapPost("/{propertyId:guid}/rooms/{roomId:guid}/beds", async (
            Guid propertyId,
            Guid roomId,
            BedWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new AddBedCommand(propertyId, roomId, request.ExpectedRoomVersion, request.Label),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.BedsManage, PropertyAccessScopeResolver.ResolverName);

        properties.MapPut("/{propertyId:guid}/rooms/{roomId:guid}/beds/{bedId:guid}", async (
            Guid propertyId,
            Guid roomId,
            Guid bedId,
            BedWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new UpdateBedCommand(propertyId, roomId, bedId, request.ExpectedRoomVersion, request.Label),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.BedsManage, PropertyAccessScopeResolver.ResolverName);

        properties.MapPost("/{propertyId:guid}/rooms/{roomId:guid}/beds/{bedId:guid}/retire", async (
            Guid propertyId,
            Guid roomId,
            Guid bedId,
            RetireBedRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<Unit> result = request.Confirmed
                ? await dispatcher.SendAsync(
                    new RetireBedCommand(propertyId, roomId, bedId, request.ExpectedRoomVersion),
                    cancellationToken).ConfigureAwait(false)
                : Result.Failure<Unit>(new("Properties.ConfirmationRequired", "Confirmation is required."));

            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(PropertiesAdminPermissionCodes.BedsManage, PropertyAccessScopeResolver.ResolverName);
    }

    public sealed record PropertyCreateRequest(string Name, string Code, string TimeZoneId);
    public sealed record PropertyUpdateRequest(string Name, string Code, string TimeZoneId, long ExpectedVersion);
    public sealed record RetirePropertyRequest(bool Confirmed, long ExpectedVersion);
    public sealed record RoomCreateRequest(
        string Name,
        long ExpectedPropertyVersion,
        string? BuildingLabel = null,
        string? FloorLabel = null);
    public sealed record RoomUpdateRequest(
        string Name,
        long ExpectedVersion,
        string? BuildingLabel = null,
        string? FloorLabel = null);
    public sealed record RetireRoomRequest(bool Confirmed, long ExpectedVersion, bool CascadeBeds = false);
    public sealed record BedWriteRequest(string Label, long ExpectedRoomVersion);
    public sealed record RetireBedRequest(bool Confirmed, long ExpectedRoomVersion);

    private static readonly ApiErrorStatusCodeMap PublicErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(PropertiesApplicationErrors.AccessDenied.Code, StatusCodes.Status403Forbidden),
        new(PropertiesApplicationErrors.PropertyNotFound.Code, StatusCodes.Status404NotFound),
        new(PropertiesApplicationErrors.RoomNotFound.Code, StatusCodes.Status404NotFound),
        new(PropertiesApplicationErrors.BedNotFound.Code, StatusCodes.Status404NotFound),
        new(PropertiesApplicationErrors.PropertyCodeAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.RoomAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.BedAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.PropertyStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.PropertyAlreadyRetired.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.PropertyRetired.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.PropertyHasActiveRooms.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.RoomStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.RoomRetired.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.RoomHasActiveBeds.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.BedStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.BedAlreadyRetired.Code, StatusCodes.Status409Conflict));
}
