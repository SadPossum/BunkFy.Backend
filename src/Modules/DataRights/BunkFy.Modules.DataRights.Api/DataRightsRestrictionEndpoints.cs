namespace BunkFy.Modules.DataRights.Api;

using BunkFy.Modules.DataRights.Application.Commands;
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

internal static class DataRightsRestrictionEndpoints
{
    public static void Map(
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
                return actor is null
                    ? Results.Unauthorized()
                    : (await dispatcher.SendAsync(
                        new ExecuteDataRightsRestrictionCommand(
                            propertyId,
                            caseId,
                            request.IdempotencyKey,
                            request.ExpectedVersion,
                            actor),
                        cancellationToken).ConfigureAwait(false))
                        .ToHttpResult(DataRightsEndpointSupport.ErrorStatusCodes);
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

    public sealed record ExecuteDataRightsRestrictionRequest(
        Guid IdempotencyKey,
        long ExpectedVersion);
}
