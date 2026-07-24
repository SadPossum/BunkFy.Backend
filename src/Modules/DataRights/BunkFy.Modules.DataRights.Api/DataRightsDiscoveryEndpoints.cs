namespace BunkFy.Modules.DataRights.Api;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class DataRightsDiscoveryEndpoints
{
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/{caseId:guid}/subjects", async (
            Guid propertyId,
            Guid caseId,
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            DataRightsSensitiveResponseHeaders.Apply(context.Response);
            return (await dispatcher.QueryAsync(
                new GetDataRightsSelectedSubjectsQuery(propertyId, caseId),
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(DataRightsEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                DataRightsAdminPermissionCodes.Discover,
                DataRightsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{caseId:guid}/subjects/discover", async (
            Guid propertyId,
            Guid caseId,
            DiscoverDataRightsSubjectsRequest request,
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            DataRightsSensitiveResponseHeaders.Apply(context.Response);
            return (await dispatcher.QueryAsync(
                new DiscoverDataRightsSubjectsQuery(
                    propertyId,
                    caseId,
                    new DataRightsSubjectLookup(
                        request.RecordId,
                        request.Email,
                        request.Phone,
                        request.Name,
                        request.DateOfBirth)),
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(DataRightsEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                DataRightsAdminPermissionCodes.Discover,
                DataRightsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{caseId:guid}/subjects/select", async (
            Guid propertyId,
            Guid caseId,
            SelectDataRightsSubjectRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            DataRightsSensitiveResponseHeaders.Apply(context.Response);
            return await DataRightsEndpointSupport.DispatchAsync(
                context,
                subjectResolver,
                actor => new SelectDataRightsSubjectCommand(
                    propertyId,
                    caseId,
                    request.Coordinate,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                DataRightsAdminPermissionCodes.Discover,
                DataRightsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{caseId:guid}/subjects/unselect", async (
            Guid propertyId,
            Guid caseId,
            UnselectDataRightsSubjectRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            DataRightsSensitiveResponseHeaders.Apply(context.Response);
            return await DataRightsEndpointSupport.DispatchAsync(
                context,
                subjectResolver,
                actor => new UnselectDataRightsSubjectCommand(
                    propertyId,
                    caseId,
                    request.Coordinate,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                DataRightsAdminPermissionCodes.Discover,
                DataRightsPropertyAccessScopeResolver.ResolverName);
    }

    public sealed record DiscoverDataRightsSubjectsRequest(
        Guid? RecordId,
        string? Email,
        string? Phone,
        string? Name,
        DateOnly? DateOfBirth);

    public sealed record SelectDataRightsSubjectRequest(
        DataRightsSubjectCoordinate Coordinate,
        long ExpectedVersion);

    public sealed record UnselectDataRightsSubjectRequest(
        DataRightsSubjectCoordinateKey Coordinate,
        long ExpectedVersion);
}
