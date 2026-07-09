namespace Properties.Api;

using Properties.Application;
using Properties.Application.Commands;
using Properties.Application.Queries;
using Properties.Contracts;
using Properties.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;

public sealed class PropertiesModule : IModule
{
    public string Name => PropertiesModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(PropertiesProfiles.Default, "Properties.Api");
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
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListPropertiesQuery(page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapGet("/{propertyId:guid}", async (
            Guid propertyId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new GetPropertyQuery(propertyId), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapPost("/", async (
            PropertyWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CreatePropertyCommand(request.Name, request.Code, request.TimeZoneId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapPut("/{propertyId:guid}", async (
            Guid propertyId,
            PropertyWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new UpdatePropertyCommand(propertyId, request.Name, request.Code, request.TimeZoneId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapGet("/{propertyId:guid}/rooms", async (
            Guid propertyId,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListRoomsQuery(propertyId, page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapPost("/{propertyId:guid}/rooms", async (
            Guid propertyId,
            RoomWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CreateRoomCommand(propertyId, request.Name, request.BuildingLabel, request.FloorLabel),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapGet("/rooms/{roomId:guid}", async (
            Guid roomId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(new GetRoomQuery(roomId), cancellationToken).ConfigureAwait(false))
            .ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapPut("/rooms/{roomId:guid}", async (
            Guid roomId,
            RoomWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new UpdateRoomCommand(roomId, request.Name, request.BuildingLabel, request.FloorLabel),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapPost("/rooms/{roomId:guid}/retire", async (
            Guid roomId,
            ConfirmedRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<Unit> result = request.Confirmed
                ? await dispatcher.SendAsync(new RetireRoomCommand(roomId), cancellationToken).ConfigureAwait(false)
                : Result.Failure<Unit>(new("Properties.ConfirmationRequired", "Confirmation is required."));

            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant();

        properties.MapGet("/rooms/{roomId:guid}/beds", async (
            Guid roomId,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListBedsQuery(roomId, page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapPost("/rooms/{roomId:guid}/beds", async (
            Guid roomId,
            BedWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new AddBedCommand(roomId, request.Label),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapPut("/rooms/{roomId:guid}/beds/{bedId:guid}", async (
            Guid roomId,
            Guid bedId,
            BedWriteRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new UpdateBedCommand(roomId, bedId, request.Label),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .RequireTenant();

        properties.MapPost("/rooms/{roomId:guid}/beds/{bedId:guid}/retire", async (
            Guid roomId,
            Guid bedId,
            ConfirmedRequest request,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Result<Unit> result = request.Confirmed
                ? await dispatcher.SendAsync(new RetireBedCommand(roomId, bedId), cancellationToken).ConfigureAwait(false)
                : Result.Failure<Unit>(new("Properties.ConfirmationRequired", "Confirmation is required."));

            return result.IsSuccess ? Results.NoContent() : result.ToHttpResult(PublicErrorStatusCodes);
        })
            .RequireTenant();
    }

    public sealed record PropertyWriteRequest(string Name, string Code, string TimeZoneId);
    public sealed record RoomWriteRequest(string Name, string? BuildingLabel = null, string? FloorLabel = null);
    public sealed record BedWriteRequest(string Label);
    public sealed record ConfirmedRequest(bool Confirmed);

    private static readonly ApiErrorStatusCodeMap PublicErrorStatusCodes = ApiErrorStatusCodeMap.Create(
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
