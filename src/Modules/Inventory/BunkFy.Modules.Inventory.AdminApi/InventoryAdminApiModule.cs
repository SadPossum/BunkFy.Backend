namespace BunkFy.Modules.Inventory.AdminApi;

using System.Security.Claims;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using BunkFy.Modules.Inventory.Admin.Contracts;
using BunkFy.Modules.Inventory.Application;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Queries;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

public sealed class InventoryAdminApiModule : IAdminApiModule
{
    public string Name => InventoryModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(InventoryProfiles.Default, "BunkFy.Modules.Inventory.AdminApi");
        builder.Services.AddInventoryApplication();
        builder.AddInventoryPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder inventory = endpoints.MapGroup("/api/admin/inventory")
            .WithModuleName(this.Name)
            .WithTags("Inventory Admin")
            .RequireAuthorization();

        inventory.MapGet("/properties/{propertyId:guid}/rooms", async (
            Guid propertyId,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.RoomsList, InventoryAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListRoomInventoryQuery(
                        propertyId,
                        page ?? PageRequest.DefaultPage,
                        pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapPut("/properties/{propertyId:guid}/rooms/{roomId:guid}/sales-mode", async (
            Guid propertyId,
            Guid roomId,
            ConfigureSalesModeRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.RoomsConfigure, InventoryAdminPermissions.Configure),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new ConfigureRoomSalesModeCommand(
                        propertyId,
                        roomId,
                        request.SalesMode,
                        request.ExpectedVersion),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapGet("/properties/{propertyId:guid}/rooms/{roomId:guid}/change-impact", async (
            Guid propertyId,
            Guid roomId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.RoomsConfigure, InventoryAdminPermissions.Configure),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetRoomInventoryChangeImpactQuery(propertyId, roomId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapGet("/properties/{propertyId:guid}/availability", async (
            Guid propertyId,
            DateOnly arrival,
            DateOnly departure,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.AvailabilityRead, InventoryAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetInventoryAvailabilityQuery(propertyId, arrival, departure), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapGet("/properties/{propertyId:guid}/blocks", async (
            Guid propertyId,
            Guid? inventoryUnitId,
            bool? includeReleased,
            int? page,
            int? pageSize,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.BlocksList, InventoryAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(
                    new ListManualInventoryBlocksQuery(
                        propertyId,
                        inventoryUnitId,
                        includeReleased ?? false,
                        page ?? PageRequest.DefaultPage,
                        pageSize ?? PageRequest.DefaultPageSize),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapPost("/properties/{propertyId:guid}/blocks", async (
            Guid propertyId,
            CreateManualBlockRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.BlocksCreate, InventoryAdminPermissions.BlocksManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new CreateManualInventoryBlockCommand(
                        propertyId,
                        request.InventoryUnitId,
                        request.Arrival,
                        request.Departure,
                        request.Reason),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapPost("/properties/{propertyId:guid}/block-groups", async (
            Guid propertyId,
            CreateManualBlockGroupRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.BlocksCreate, InventoryAdminPermissions.BlocksManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new CreateManualInventoryBlockGroupCommand(
                        propertyId,
                        request.Target,
                        request.Arrival,
                        request.Departure,
                        request.Reason),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapPost("/properties/{propertyId:guid}/blocks/{blockId:guid}/release", async (
            Guid propertyId,
            Guid blockId,
            ReleaseManualBlockRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.BlocksRelease, InventoryAdminPermissions.BlocksManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new ReleaseManualInventoryBlockCommand(propertyId, blockId, request.ExpectedVersion),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapPost("/properties/{propertyId:guid}/block-groups/{blockGroupId:guid}/release", async (
            Guid propertyId,
            Guid blockGroupId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.BlocksRelease, InventoryAdminPermissions.BlocksManage),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new ReleaseManualInventoryBlockGroupCommand(propertyId, blockGroupId),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapPost("/properties/{propertyId:guid}/rooms/{roomId:guid}/beds/{bedId:guid}/retirement", async (
            Guid propertyId,
            Guid roomId,
            Guid bedId,
            RequestBedRetirementRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.BedRetirementsRequest, InventoryAdminPermissions.Configure),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new RequestBedRetirementCommand(
                        propertyId,
                        roomId,
                        bedId,
                        request.Reason,
                        Actor(httpContext)),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapGet("/properties/{propertyId:guid}/bed-retirements/{topologyChangeId:guid}", async (
            Guid propertyId,
            Guid topologyChangeId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.BedRetirementsGet, InventoryAdminPermissions.Configure),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetBedRetirementQuery(propertyId, topologyChangeId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapPost("/properties/{propertyId:guid}/bed-retirements/{topologyChangeId:guid}/retry", async (
            Guid propertyId,
            Guid topologyChangeId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.BedRetirementsRetry, InventoryAdminPermissions.Configure),
                requireTenant: true,
                token => dispatcher.SendAsync(new RetryBedRetirementCommand(propertyId, topologyChangeId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapPost("/properties/{propertyId:guid}/rooms/{roomId:guid}/retirement", async (
            Guid propertyId,
            Guid roomId,
            RequestRoomRetirementRequest request,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.RoomRetirementsRequest, InventoryAdminPermissions.Configure),
                requireTenant: true,
                token => dispatcher.SendAsync(
                    new RequestRoomRetirementCommand(
                        propertyId,
                        roomId,
                        request.Reason,
                        Actor(httpContext)),
                    token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapGet("/properties/{propertyId:guid}/room-retirements/{topologyChangeId:guid}", async (
            Guid propertyId,
            Guid topologyChangeId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.RoomRetirementsGet, InventoryAdminPermissions.Configure),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetRoomRetirementQuery(propertyId, topologyChangeId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));

        inventory.MapPost("/properties/{propertyId:guid}/room-retirements/{topologyChangeId:guid}/retry", async (
            Guid propertyId,
            Guid topologyChangeId,
            HttpContext httpContext,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await executor.ExecuteAsync(
                httpContext,
                AdminOperation.Create(InventoryAdminOperationNames.RoomRetirementsRetry, InventoryAdminPermissions.Configure),
                requireTenant: true,
                token => dispatcher.SendAsync(new RetryRoomRetirementCommand(propertyId, topologyChangeId), token),
                cancellationToken,
                errorStatusCodes: AdminErrorStatusCodes).ConfigureAwait(false));
    }

    public sealed record ConfigureSalesModeRequest(InventorySalesMode SalesMode, long ExpectedVersion);
    public sealed record CreateManualBlockRequest(
        Guid InventoryUnitId,
        DateOnly Arrival,
        DateOnly Departure,
        string Reason);
    public sealed record CreateManualBlockGroupRequest(
        InventoryBlockTarget Target,
        DateOnly Arrival,
        DateOnly Departure,
        string Reason);
    public sealed record ReleaseManualBlockRequest(long ExpectedVersion);
    public sealed record RequestBedRetirementRequest(string Reason);
    public sealed record RequestRoomRetirementRequest(string Reason);

    private static readonly ApiErrorStatusCodeMap AdminErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(InventoryApplicationErrors.PropertyNotFound.Code, StatusCodes.Status404NotFound),
        new(InventoryApplicationErrors.RoomNotFound.Code, StatusCodes.Status404NotFound),
        new(InventoryApplicationErrors.RoomRetired.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.BedLevelRequiresBeds.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.SalesModeInvalid.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.RoomHasActiveClaims.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.InventoryUnitNotFound.Code, StatusCodes.Status404NotFound),
        new(InventoryApplicationErrors.InventoryUnitInactive.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.InventoryUnitNotSellable.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.BlockNotFound.Code, StatusCodes.Status404NotFound),
        new(InventoryApplicationErrors.BlockGroupNotFound.Code, StatusCodes.Status404NotFound),
        new(InventoryApplicationErrors.BlockTargetInvalid.Code, StatusCodes.Status400BadRequest),
        new(InventoryApplicationErrors.BlockTargetEmpty.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.BlockOverlap.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.BlockAllocationConflict.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.StayRangeInvalid.Code, StatusCodes.Status400BadRequest),
        new(InventoryApplicationErrors.BlockReasonInvalid.Code, StatusCodes.Status400BadRequest),
        new(InventoryApplicationErrors.BlockAlreadyReleased.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.BedRetirementNotFound.Code, StatusCodes.Status404NotFound),
        new(InventoryApplicationErrors.BedRetirementRetryInvalid.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.BedRetirementStillDraining.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.BedRetirementInProgress.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.RoomRetirementNotFound.Code, StatusCodes.Status404NotFound),
        new(InventoryApplicationErrors.RoomRetirementRetryInvalid.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.RoomRetirementStillDraining.Code, StatusCodes.Status409Conflict),
        new(InventoryApplicationErrors.RoomRetirementInProgress.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Inventory.Domain.Errors.InventoryDomainErrors.BedRetirementRequestInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Inventory.Domain.Errors.InventoryDomainErrors.BedRetirementTransitionInvalid.Code, StatusCodes.Status409Conflict),
        new(BunkFy.Modules.Inventory.Domain.Errors.InventoryDomainErrors.RoomRetirementRequestInvalid.Code, StatusCodes.Status400BadRequest),
        new(BunkFy.Modules.Inventory.Domain.Errors.InventoryDomainErrors.RoomRetirementTransitionInvalid.Code, StatusCodes.Status409Conflict));

    private static string Actor(HttpContext context)
    {
        string identity = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.Identity?.Name
            ?? $"authenticated:{context.User.Identity?.AuthenticationType ?? "unknown"}";
        return $"admin-api:{identity}";
    }
}
