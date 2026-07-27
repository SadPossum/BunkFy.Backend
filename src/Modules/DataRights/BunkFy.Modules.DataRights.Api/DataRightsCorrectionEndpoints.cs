namespace BunkFy.Modules.DataRights.Api;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class DataRightsCorrectionEndpoints
{
    public static void Map(
        RouteGroupBuilder group,
        AuthenticationAssuranceRequirement? executionAssurance)
    {
        group.MapGet(
            "/{caseId:guid}/correction",
            async (
                Guid propertyId,
                Guid caseId,
                HttpContext context,
                IRequestDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                DataRightsSensitiveResponseHeaders.Apply(context.Response);
                return (await dispatcher.QueryAsync(
                    new GetDataRightsCorrectionExecutionQuery(propertyId, caseId),
                    cancellationToken).ConfigureAwait(false))
                    .ToHttpResult(DataRightsEndpointSupport.ErrorStatusCodes);
            })
            .Produces<DataRightsCorrectionExecutionDetailsDto>()
            .RequireTenant()
            .RequireResolvedScopePermission(
                DataRightsAdminPermissionCodes.Execute,
                DataRightsPropertyAccessScopeResolver.ResolverName);

        RouteHandlerBuilder start = group.MapPost(
            "/{caseId:guid}/correction",
            async (
                Guid propertyId,
                Guid caseId,
                StartDataRightsCorrectionRequest request,
                HttpContext context,
                IAccessHttpSubjectResolver subjectResolver,
                IRequestDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                DataRightsSensitiveResponseHeaders.Apply(context.Response);
                string? actor = DataRightsEndpointSupport.ResolveActor(
                    context,
                    subjectResolver);
                return actor is null
                    ? Results.Unauthorized()
                    : (await dispatcher.SendAsync(
                        new StartDataRightsCorrectionExecutionCommand(
                            propertyId,
                            caseId,
                            request.ExecutionId,
                            request.ExpectedVersion,
                            actor),
                        cancellationToken).ConfigureAwait(false))
                        .ToHttpResult(DataRightsEndpointSupport.ErrorStatusCodes);
            })
            .Produces<DataRightsCorrectionExecutionDto>()
            .RequireTenant()
            .RequireResolvedScopePermission(
                DataRightsAdminPermissionCodes.Execute,
                DataRightsPropertyAccessScopeResolver.ResolverName);
        if (executionAssurance is not null)
        {
            start.RequireAuthenticationAssurance(executionAssurance);
        }
    }

    public sealed record StartDataRightsCorrectionRequest(
        Guid ExecutionId,
        long ExpectedVersion);
}
