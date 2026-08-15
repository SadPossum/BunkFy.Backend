namespace BunkFy.Modules.DataRights.Api;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class DataRightsRestrictionEndpoints
{
    public static void MapProperty(
        RouteGroupBuilder group,
        AuthenticationAssuranceRequirement? executionAssurance)
    {
        RouteHandlerBuilder execute = group.MapPost(
            "/{caseId:guid}/restriction",
            async (
                Guid propertyId,
                Guid caseId,
                ExecuteDataRightsRestrictionRequest request,
                HttpContext context,
                IAccessHttpSubjectResolver subjectResolver,
                IRequestDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                DataRightsSensitiveResponseHeaders.Apply(context.Response);
                string? actor = DataRightsEndpointSupport.ResolveActor(
                    context,
                    subjectResolver);
                if (actor is null)
                {
                    return Results.Unauthorized();
                }

                Result<DataRightsRestrictionExecutionDto> result =
                    await dispatcher.SendAsync(
                        new ExecuteDataRightsRestrictionCommand(
                            DataRightsCaseScope.ForProperty(propertyId),
                            caseId,
                            request.IdempotencyKey,
                            request.ExpectedVersion,
                            actor),
                        cancellationToken).ConfigureAwait(false);
                return DataRightsEndpointSupport.ToHttpResult(context, result);
            })
            .Produces<DataRightsRestrictionExecutionDto>()
            .RequireTenant()
            .RequireResolvedScopePermission(
                DataRightsAdminPermissionCodes.Restrict,
                DataRightsPropertyAccessScopeResolver.ResolverName);
        if (executionAssurance is not null)
        {
            execute.RequireAuthenticationAssurance(executionAssurance);
        }
    }

    public static void MapTenant(
        RouteGroupBuilder group,
        AuthenticationAssuranceRequirement? executionAssurance)
    {
        RouteHandlerBuilder execute = group.MapPost(
            "/{caseId:guid}/restriction",
            async (
                Guid caseId,
                ExecuteDataRightsRestrictionRequest request,
                HttpContext context,
                IAccessHttpSubjectResolver subjectResolver,
                IRequestDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                DataRightsSensitiveResponseHeaders.Apply(context.Response);
                string? actor = DataRightsEndpointSupport.ResolveActor(
                    context,
                    subjectResolver);
                if (actor is null)
                {
                    return Results.Unauthorized();
                }

                Result<DataRightsRestrictionExecutionDto> result =
                    await dispatcher.SendAsync(
                        new ExecuteDataRightsRestrictionCommand(
                            DataRightsCaseScope.Staff,
                            caseId,
                            request.IdempotencyKey,
                            request.ExpectedVersion,
                            actor),
                        cancellationToken).ConfigureAwait(false);
                return DataRightsEndpointSupport.ToHttpResult(context, result);
            })
            .Produces<DataRightsRestrictionExecutionDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Restrict);
        if (executionAssurance is not null)
        {
            execute.RequireAuthenticationAssurance(executionAssurance);
        }
    }

    public sealed record ExecuteDataRightsRestrictionRequest(
        Guid IdempotencyKey,
        long ExpectedVersion);
}
