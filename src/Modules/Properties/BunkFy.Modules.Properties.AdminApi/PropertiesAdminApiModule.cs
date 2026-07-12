namespace BunkFy.Modules.Properties.AdminApi;

using BunkFy.Modules.Properties.Admin.Contracts;
using BunkFy.Modules.Properties.Application;
using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Queries;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

public sealed class PropertiesAdminApiModule : IAdminApiModule
{
    public string Name => PropertiesModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(PropertiesProfiles.Default, "BunkFy.Modules.Properties.AdminApi");
        builder.Services.AddPropertiesApplication();
        builder.AddPropertiesPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder properties = endpoints.MapGroup("/api/admin/properties")
            .WithModuleName(this.Name)
            .WithTags("Properties Admin")
            .RequireAuthorization();

        properties.MapGet("/", async (
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesList, PropertiesAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListPropertiesQuery(page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken).ConfigureAwait(false));

        properties.MapGet("/{propertyId:guid}", async (
            Guid propertyId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesGet, PropertiesAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetPropertyQuery(propertyId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPost("/", async (
            PropertyCreateRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesCreate, PropertiesAdminPermissions.PropertiesManage),
                requireTenant: true,
                token => dispatcher.SendAsync(new CreatePropertyCommand(request.Name, request.Code, request.TimeZoneId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPut("/{propertyId:guid}", async (
            Guid propertyId,
            PropertyUpdateRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesUpdate, PropertiesAdminPermissions.PropertiesManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new UpdatePropertyCommand(propertyId, request.Name, request.Code, request.TimeZoneId, request.ExpectedVersion),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPost("/{propertyId:guid}/retire", async (
            Guid propertyId,
            RetirePropertyRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesRetire, PropertiesAdminPermissions.PropertiesManage),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new RetirePropertyCommand(propertyId, request.ExpectedVersion), token)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapGet("/{propertyId:guid}/rooms", async (
            Guid propertyId,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsList, PropertiesAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListRoomsQuery(propertyId, page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPost("/{propertyId:guid}/rooms", async (
            Guid propertyId,
            RoomCreateRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsCreate, PropertiesAdminPermissions.RoomsManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new CreateRoomCommand(
                        propertyId,
                        request.ExpectedPropertyVersion,
                        request.Name,
                        request.BuildingLabel,
                        request.FloorLabel),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapGet("/{propertyId:guid}/rooms/{roomId:guid}", async (
            Guid propertyId,
            Guid roomId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsGet, PropertiesAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetRoomQuery(propertyId, roomId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPut("/{propertyId:guid}/rooms/{roomId:guid}", async (
            Guid propertyId,
            Guid roomId,
            RoomUpdateRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsUpdate, PropertiesAdminPermissions.RoomsManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new UpdateRoomCommand(
                        propertyId,
                        roomId,
                        request.ExpectedVersion,
                        request.Name,
                        request.BuildingLabel,
                        request.FloorLabel),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPost("/{propertyId:guid}/rooms/{roomId:guid}/retire", async (
            Guid propertyId,
            Guid roomId,
            RetireRoomRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsRetire, PropertiesAdminPermissions.RoomsManage),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(
                        new RetireRoomCommand(propertyId, roomId, request.ExpectedVersion, request.CascadeBeds),
                        token)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapGet("/{propertyId:guid}/rooms/{roomId:guid}/beds", async (
            Guid propertyId,
            Guid roomId,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.BedsList, PropertiesAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListBedsQuery(propertyId, roomId, page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPost("/{propertyId:guid}/rooms/{roomId:guid}/beds", async (
            Guid propertyId,
            Guid roomId,
            BedWriteRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.BedsAdd, PropertiesAdminPermissions.BedsManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new AddBedCommand(propertyId, roomId, request.ExpectedRoomVersion, request.Label),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPut("/{propertyId:guid}/rooms/{roomId:guid}/beds/{bedId:guid}", async (
            Guid propertyId,
            Guid roomId,
            Guid bedId,
            BedWriteRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.BedsUpdate, PropertiesAdminPermissions.BedsManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new UpdateBedCommand(propertyId, roomId, bedId, request.ExpectedRoomVersion, request.Label),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPost("/{propertyId:guid}/rooms/{roomId:guid}/beds/{bedId:guid}/retire", async (
            Guid propertyId,
            Guid roomId,
            Guid bedId,
            RetireBedRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.BedsRetire, PropertiesAdminPermissions.BedsManage),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(
                        new RetireBedCommand(propertyId, roomId, bedId, request.ExpectedRoomVersion),
                        token)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));
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

    private static readonly ApiErrorStatusCodeMap AdminErrorStatusCodes = ApiErrorStatusCodeMap.Create(
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
