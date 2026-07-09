namespace Properties.AdminApi;

using Properties.Admin.Contracts;
using Properties.Application;
using Properties.Application.Commands;
using Properties.Application.Queries;
using Properties.Contracts;
using Properties.Persistence;
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
        builder.SelectModuleProfile(PropertiesProfiles.Default, "Properties.AdminApi");
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
            PropertyWriteRequest request,
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
            PropertyWriteRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.PropertiesUpdate, PropertiesAdminPermissions.PropertiesManage),
                requireTenant: true,
                token => dispatcher.SendAsync(new UpdatePropertyCommand(propertyId, request.Name, request.Code, request.TimeZoneId), token),
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
            RoomWriteRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsCreate, PropertiesAdminPermissions.RoomsManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new CreateRoomCommand(propertyId, request.Name, request.BuildingLabel, request.FloorLabel),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapGet("/rooms/{roomId:guid}", async (
            Guid roomId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsGet, PropertiesAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetRoomQuery(roomId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPut("/rooms/{roomId:guid}", async (
            Guid roomId,
            RoomWriteRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsUpdate, PropertiesAdminPermissions.RoomsManage),
                requireTenant: true,
                token => dispatcher.SendAsync(new UpdateRoomCommand(roomId, request.Name, request.BuildingLabel, request.FloorLabel), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPost("/rooms/{roomId:guid}/retire", async (
            Guid roomId,
            ConfirmedRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.RoomsRetire, PropertiesAdminPermissions.RoomsManage),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new RetireRoomCommand(roomId), token)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapGet("/rooms/{roomId:guid}/beds", async (
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
                    new ListBedsQuery(roomId, page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPost("/rooms/{roomId:guid}/beds", async (
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
                token => dispatcher.SendAsync(new AddBedCommand(roomId, request.Label), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPut("/rooms/{roomId:guid}/beds/{bedId:guid}", async (
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
                token => dispatcher.SendAsync(new UpdateBedCommand(roomId, bedId, request.Label), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        properties.MapPost("/rooms/{roomId:guid}/beds/{bedId:guid}/retire", async (
            Guid roomId,
            Guid bedId,
            ConfirmedRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(PropertiesAdminOperationNames.BedsRetire, PropertiesAdminPermissions.BedsManage),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new RetireBedCommand(roomId, bedId), token)
                    : Task.FromResult(Result.Failure<Unit>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));
    }

    public sealed record PropertyWriteRequest(string Name, string Code, string TimeZoneId);
    public sealed record RoomWriteRequest(string Name, string? BuildingLabel = null, string? FloorLabel = null);
    public sealed record BedWriteRequest(string Label);
    public sealed record ConfirmedRequest(bool Confirmed);

    private static readonly ApiErrorStatusCodeMap AdminErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(PropertiesApplicationErrors.PropertyNotFound.Code, StatusCodes.Status404NotFound),
        new(PropertiesApplicationErrors.RoomNotFound.Code, StatusCodes.Status404NotFound),
        new(PropertiesApplicationErrors.BedNotFound.Code, StatusCodes.Status404NotFound),
        new(PropertiesApplicationErrors.PropertyCodeAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.RoomAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.BedAlreadyExists.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.PropertyStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.RoomStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.RoomRetired.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.BedStatusUnknown.Code, StatusCodes.Status409Conflict),
        new(PropertiesApplicationErrors.BedAlreadyRetired.Code, StatusCodes.Status409Conflict));
}
