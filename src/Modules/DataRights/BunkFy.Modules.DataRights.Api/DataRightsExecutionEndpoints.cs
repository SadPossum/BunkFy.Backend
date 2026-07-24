namespace BunkFy.Modules.DataRights.Api;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.Permissions;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class DataRightsExecutionEndpoints
{
    public static void Map(
        RouteGroupBuilder group,
        AuthenticationAssuranceRequirement? executionAssurance)
    {
        group.MapGet("/{caseId:guid}/execution", async (
            Guid propertyId,
            Guid caseId,
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            DataRightsSensitiveResponseHeaders.Apply(context.Response);
            return (await dispatcher.QueryAsync(
                new GetDataRightsExecutionQuery(propertyId, caseId),
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(DataRightsEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                DataRightsAdminPermissionCodes.Read,
                DataRightsPropertyAccessScopeResolver.ResolverName);

        RouteHandlerBuilder startExecution = group.MapPost(
            "/{caseId:guid}/execution",
            async (
                Guid propertyId,
                Guid caseId,
                StartDataRightsExecutionRequest request,
                HttpContext context,
                IAccessHttpSubjectResolver subjectResolver,
                IRequestDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                DataRightsSensitiveResponseHeaders.Apply(context.Response);
                string? actor = DataRightsEndpointSupport.ResolveActor(context, subjectResolver);
                return actor is null
                    ? Results.Unauthorized()
                    : (await dispatcher.SendAsync(
                        new StartDataRightsAnonymisationExecutionCommand(
                            propertyId,
                            caseId,
                            request.IdempotencyKey,
                            request.ExpectedVersion,
                            actor),
                        cancellationToken).ConfigureAwait(false))
                        .ToHttpResult(DataRightsEndpointSupport.ErrorStatusCodes);
            })
            .RequireTenant()
            .RequireAllPermissions(
                TenantAccessPermissionMetadata.Create(
                    DataRightsAdminPermissionCodes.Erase),
                new AccessPermissionMetadata(
                    PermissionCode.Create(DataRightsAdminPermissionCodes.Read),
                    scopeResolverName: DataRightsPropertyAccessScopeResolver.ResolverName,
                    requireScope: true));

        startExecution.RequireAssuranceWhenConfigured(executionAssurance);
    }

    public sealed record StartDataRightsExecutionRequest(
        Guid IdempotencyKey,
        long ExpectedVersion);

    private static RouteHandlerBuilder RequireAssuranceWhenConfigured(
        this RouteHandlerBuilder endpoint,
        AuthenticationAssuranceRequirement? requirement) =>
        requirement is null
            ? endpoint
            : endpoint.RequireAuthenticationAssurance(requirement);
}
