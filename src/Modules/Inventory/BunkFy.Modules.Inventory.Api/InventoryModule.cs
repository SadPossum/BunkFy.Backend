namespace BunkFy.Modules.Inventory.Api;

using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using BunkFy.Modules.Inventory.Application;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Queries;
using BunkFy.Modules.Inventory.Contracts;
using BunkFy.Modules.Inventory.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public sealed class InventoryModule : IModule
{
    public string Name => InventoryModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(InventoryProfiles.Default, "BunkFy.Modules.Inventory.Api");
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAccessHttpScopeResolver, InventoryPropertyAccessScopeResolver>());
        builder.Services.AddInventoryApplication();
        builder.AddInventoryPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder inventory = endpoints.MapGroup("/api/inventory")
            .WithModuleName(this.Name)
            .WithTags("Inventory")
            .RequireAuthorization();

        inventory.MapGet("/properties/{propertyId:guid}/rooms", async (
            Guid propertyId,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListRoomInventoryQuery(
                    propertyId,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<RoomInventoryListResponse>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Read,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapPut("/properties/{propertyId:guid}/rooms/{roomId:guid}/sales-mode", async (
            Guid propertyId,
            Guid roomId,
            ConfigureSalesModeRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new ConfigureRoomSalesModeCommand(
                    propertyId,
                    roomId,
                    request.SalesMode,
                    request.ExpectedVersion,
                    ResolveActor(httpContext, subjectResolver)),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<RoomInventoryDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Configure,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapGet("/properties/{propertyId:guid}/rooms/{roomId:guid}/change-impact", async (
            Guid propertyId,
            Guid roomId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetRoomInventoryChangeImpactQuery(propertyId, roomId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<RoomInventoryChangeImpactDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Configure,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapGet("/properties/{propertyId:guid}/availability", async (
            Guid propertyId,
            DateOnly arrival,
            DateOnly departure,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetInventoryAvailabilityQuery(propertyId, arrival, departure),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<InventoryAvailabilityResponse>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Read,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapGet("/properties/{propertyId:guid}/blocks", async (
            Guid propertyId,
            Guid? inventoryUnitId,
            bool? includeReleased,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListManualInventoryBlocksQuery(
                    propertyId,
                    inventoryUnitId,
                    includeReleased ?? false,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<ManualInventoryBlockListResponse>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Read,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapPost("/properties/{propertyId:guid}/blocks", async (
            Guid propertyId,
            CreateManualBlockRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CreateManualInventoryBlockCommand(
                    propertyId,
                    request.InventoryUnitId,
                    request.Arrival,
                    request.Departure,
                    request.Reason,
                    ResolveActor(httpContext, subjectResolver)),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<ManualInventoryBlockDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.BlocksManage,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapPost("/properties/{propertyId:guid}/block-groups", async (
            Guid propertyId,
            CreateManualBlockGroupRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new CreateManualInventoryBlockGroupCommand(
                    propertyId,
                    request.Target,
                    request.Arrival,
                    request.Departure,
                    request.Reason,
                    ResolveActor(httpContext, subjectResolver)),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<ManualInventoryBlockGroupDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.BlocksManage,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapPost("/properties/{propertyId:guid}/blocks/{blockId:guid}/release", async (
            Guid propertyId,
            Guid blockId,
            ReleaseManualBlockRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new ReleaseManualInventoryBlockCommand(
                    propertyId,
                    blockId,
                    request.ExpectedVersion,
                    ResolveActor(httpContext, subjectResolver)),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<ManualInventoryBlockDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.BlocksManage,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapPost("/properties/{propertyId:guid}/block-groups/{blockGroupId:guid}/release", async (
            Guid propertyId,
            Guid blockGroupId,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new ReleaseManualInventoryBlockGroupCommand(
                    propertyId,
                    blockGroupId,
                    ResolveActor(httpContext, subjectResolver)),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<ManualInventoryBlockGroupDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.BlocksManage,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapPost("/properties/{propertyId:guid}/rooms/{roomId:guid}/beds/{bedId:guid}/retirement", async (
            Guid propertyId,
            Guid roomId,
            Guid bedId,
            RequestBedRetirementRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Gma.Framework.AccessControl.AccessSubject? subject = subjectResolver.ResolveSubject(httpContext);
            return subject is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new RequestBedRetirementCommand(
                        propertyId,
                        roomId,
                        bedId,
                        request.Reason,
                        $"{Gma.Framework.AccessControl.AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}"),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes);
        })
            .Produces<BedRetirementDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Configure,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapGet("/properties/{propertyId:guid}/bed-retirements/{topologyChangeId:guid}", async (
            Guid propertyId,
            Guid topologyChangeId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetBedRetirementQuery(propertyId, topologyChangeId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<BedRetirementDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Configure,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapPost("/properties/{propertyId:guid}/bed-retirements/{topologyChangeId:guid}/retry", async (
            Guid propertyId,
            Guid topologyChangeId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new RetryBedRetirementCommand(propertyId, topologyChangeId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<BedRetirementDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Configure,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapPost("/properties/{propertyId:guid}/rooms/{roomId:guid}/retirement", async (
            Guid propertyId,
            Guid roomId,
            RequestRoomRetirementRequest request,
            HttpContext httpContext,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            Gma.Framework.AccessControl.AccessSubject? subject = subjectResolver.ResolveSubject(httpContext);
            return subject is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new RequestRoomRetirementCommand(
                        propertyId,
                        roomId,
                        request.Reason,
                        $"{Gma.Framework.AccessControl.AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}"),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes);
        })
            .Produces<RoomRetirementDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Configure,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapGet("/properties/{propertyId:guid}/room-retirements/{topologyChangeId:guid}", async (
            Guid propertyId,
            Guid topologyChangeId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetRoomRetirementQuery(propertyId, topologyChangeId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<RoomRetirementDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Configure,
                InventoryPropertyAccessScopeResolver.ResolverName);

        inventory.MapPost("/properties/{propertyId:guid}/room-retirements/{topologyChangeId:guid}/retry", async (
            Guid propertyId,
            Guid topologyChangeId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.SendAsync(
                new RetryRoomRetirementCommand(propertyId, topologyChangeId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(PublicErrorStatusCodes))
            .Produces<RoomRetirementDto>(StatusCodes.Status200OK)
            .RequireTenant()
            .RequireResolvedScopePermission(
                InventoryAdminPermissionCodes.Configure,
                InventoryPropertyAccessScopeResolver.ResolverName);
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

    private static string? ResolveActor(HttpContext context, IAccessHttpSubjectResolver subjectResolver)
    {
        Gma.Framework.AccessControl.AccessSubject? subject = subjectResolver.ResolveSubject(context);
        return subject is null
            ? null
            : $"{Gma.Framework.AccessControl.AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}";
    }

    private static readonly ApiErrorStatusCodeMap PublicErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(InventoryApplicationErrors.AccessDenied.Code, StatusCodes.Status403Forbidden),
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
}
