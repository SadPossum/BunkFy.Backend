namespace BunkFy.Modules.DataRights.Api;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Security;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class DataRightsTenantEndpoints
{
    public static void Map(
        IEndpointRouteBuilder endpoints,
        string moduleName,
        AuthenticationAssuranceRequirement? exportGenerationAssurance,
        AuthenticationAssuranceRequirement? exportDownloadAssurance)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/data-rights/tenant/cases")
            .WithModuleName(moduleName)
            .WithTags("Data rights")
            .RequireAuthorization();

        group.MapGet("", async (
            DataRightsCaseStatus? status,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListDataRightsCasesQuery(
                    DataRightsCaseScope.Staff,
                    status,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(
                    DataRightsEndpointSupport.ErrorStatusCodes))
            .Produces<DataRightsCaseListResponse>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Read);

        group.MapGet("/{caseId:guid}", async (
            Guid caseId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetDataRightsCaseQuery(DataRightsCaseScope.Staff, caseId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(
                    DataRightsEndpointSupport.ErrorStatusCodes))
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Read);

        group.MapPost("", async (
            DataRightsModule.CreateDataRightsCaseRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string? actor = DataRightsEndpointSupport.ResolveActor(context, subjectResolver);
            return actor is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new CreateDataRightsCaseCommand(
                        DataRightsCaseScope.Staff,
                        request.RequestedOperations,
                        request.RestrictionDirective,
                        request.RequesterRelationship,
                        actor),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(
                        DataRightsEndpointSupport.ErrorStatusCodes);
        })
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Create);

        group.MapPost("/{caseId:guid}/requester-verification", async (
            Guid caseId,
            DataRightsModule.RecordRequesterVerificationRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await DataRightsEndpointSupport.DispatchAsync(
                context,
                subjectResolver,
                actor => new RecordRequesterVerificationCommand(
                    DataRightsCaseScope.Staff,
                    caseId,
                    request.Verified,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false))
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Review);

        group.MapPost("/{caseId:guid}/controller-routing", async (
            Guid caseId,
            DataRightsModule.VersionedDataRightsCaseRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await DataRightsEndpointSupport.DispatchAsync(
                context,
                subjectResolver,
                actor => new RecordControllerRoutingCommand(
                    DataRightsCaseScope.Staff,
                    caseId,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false))
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Review);

        group.MapPost("/{caseId:guid}/discovery", async (
            Guid caseId,
            DataRightsModule.VersionedDataRightsCaseRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await DataRightsEndpointSupport.DispatchAsync(
                context,
                subjectResolver,
                actor => new BeginDataRightsDiscoveryCommand(
                    DataRightsCaseScope.Staff,
                    caseId,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false))
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Discover);

        group.MapPost("/{caseId:guid}/review", async (
            Guid caseId,
            DataRightsModule.VersionedDataRightsCaseRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await DataRightsEndpointSupport.DispatchAsync(
                context,
                subjectResolver,
                actor => new RequireDataRightsReviewCommand(
                    DataRightsCaseScope.Staff,
                    caseId,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false))
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Review);

        group.MapPost("/{caseId:guid}/decision", async (
            Guid caseId,
            DataRightsModule.VersionedDataRightsCaseRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await DataRightsEndpointSupport.DispatchAsync(
                context,
                subjectResolver,
                actor => new BeginDataRightsDecisionCommand(
                    DataRightsCaseScope.Staff,
                    caseId,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false))
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Decide);

        group.MapPost("/{caseId:guid}/decision/outcome", async (
            Guid caseId,
            DataRightsModule.RecordDataRightsDecisionRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await DataRightsEndpointSupport.DispatchAsync(
                context,
                subjectResolver,
                actor => new RecordDataRightsDecisionCommand(
                    DataRightsCaseScope.Staff,
                    caseId,
                    request.Decision,
                    request.Reason,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false))
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Decide);

        group.MapPost("/{caseId:guid}/cancel", async (
            Guid caseId,
            DataRightsModule.VersionedDataRightsCaseRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            await DataRightsEndpointSupport.DispatchAsync(
                context,
                subjectResolver,
                actor => new CancelDataRightsCaseCommand(
                    DataRightsCaseScope.Staff,
                    caseId,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false))
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Manage);

        MapDiscovery(group);
        DataRightsExportEndpoints.MapTenant(
            group,
            exportGenerationAssurance,
            exportDownloadAssurance);
    }

    private static void MapDiscovery(RouteGroupBuilder group)
    {
        group.MapGet("/{caseId:guid}/subjects", async (
            Guid caseId,
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            DataRightsSensitiveResponseHeaders.Apply(context.Response);
            return (await dispatcher.QueryAsync(
                new GetDataRightsSelectedSubjectsQuery(
                    DataRightsCaseScope.Staff,
                    caseId),
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(DataRightsEndpointSupport.ErrorStatusCodes);
        })
            .Produces<DataRightsSelectedSubjectsResponse>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Discover);

        group.MapPost("/{caseId:guid}/subjects/discover", async (
            Guid caseId,
            DataRightsDiscoveryEndpoints.DiscoverDataRightsSubjectsRequest request,
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            DataRightsSensitiveResponseHeaders.Apply(context.Response);
            return (await dispatcher.QueryAsync(
                new DiscoverDataRightsSubjectsQuery(
                    DataRightsCaseScope.Staff,
                    caseId,
                    new DataRightsSubjectLookup(
                        request.RecordId,
                        request.Email,
                        request.Phone,
                        request.Name,
                        request.DateOfBirth,
                        request.AccountSubjectId),
                    request.OwnerKey),
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(DataRightsEndpointSupport.ErrorStatusCodes);
        })
            .Produces<DataRightsSubjectDiscoveryResponse>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Discover);

        group.MapPost("/{caseId:guid}/subjects/select", async (
            Guid caseId,
            DataRightsDiscoveryEndpoints.SelectDataRightsSubjectRequest request,
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
                    DataRightsCaseScope.Staff,
                    caseId,
                    request.Coordinate,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false);
        })
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Discover);

        group.MapPost("/{caseId:guid}/subjects/unselect", async (
            Guid caseId,
            DataRightsDiscoveryEndpoints.UnselectDataRightsSubjectRequest request,
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
                    DataRightsCaseScope.Staff,
                    caseId,
                    request.Coordinate,
                    request.ExpectedVersion,
                    actor),
                dispatcher,
                cancellationToken).ConfigureAwait(false);
        })
            .Produces<DataRightsCaseDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Discover);
    }
}
