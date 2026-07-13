namespace BunkFy.Host.Api;

using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Permissions;
using Gma.Framework.Scoping;

internal static class AccessPermissionEndpoints
{
    private const int MaximumChecks = 32;

    public static IEndpointRouteBuilder MapBunkFyAccessPermissionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/access/permissions/evaluate", EvaluateAsync)
            .WithTags("Access")
            .Produces<AccessPermissionEvaluationResponse>(StatusCodes.Status200OK)
            .RequireAuthorization()
            .RequireTenant();

        return endpoints;
    }

    private static async Task<IResult> EvaluateAsync(
        AccessPermissionEvaluationRequest request,
        HttpContext httpContext,
        IScopeContext scopeContext,
        IAccessHttpSubjectResolver subjectResolver,
        IAccessAuthorizationService authorizationService,
        CancellationToken cancellationToken)
    {
        if (request.Checks is not { Count: > 0 and <= MaximumChecks })
        {
            return Results.BadRequest(new
            {
                Error = $"Between 1 and {MaximumChecks} permission checks are required."
            });
        }

        AccessSubject? subject = subjectResolver.ResolveSubject(httpContext);
        if (subject is null)
        {
            return Results.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Results.BadRequest(new { Error = "A tenant scope is required." });
        }

        List<PreparedPermissionCheck> prepared = [];
        foreach (AccessPermissionCheck check in request.Checks)
        {
            if (!PermissionCode.TryCreate(check.Permission, out PermissionCode? permission) ||
                !AccessScope.TryParse(check.Scope, out AccessScope? accessScope) ||
                !BelongsToTenant(accessScope, scopeContext.ScopeId))
            {
                return Results.BadRequest(new
                {
                    Error = "Each check requires a valid permission and a tenant-owned access scope."
                });
            }

            prepared.Add(new PreparedPermissionCheck(permission, accessScope));
        }

        List<AccessPermissionDecision> decisions = new(prepared.Count);
        foreach (PreparedPermissionCheck check in prepared)
        {
            AccessDecision decision = await authorizationService.AuthorizeAsync(
                new AccessRequirement(subject, check.Permission, check.Scope),
                cancellationToken).ConfigureAwait(false);
            decisions.Add(new AccessPermissionDecision(
                check.Permission.Value,
                check.Scope.Value,
                decision.IsAllowed));
        }

        return Results.Ok(new AccessPermissionEvaluationResponse(decisions));
    }

    private static bool BelongsToTenant(AccessScope scope, string tenantId) =>
        scope.Segments is [{ Name: "tenant" } tenantSegment, ..] &&
        string.Equals(tenantSegment.Value, tenantId, StringComparison.Ordinal);

    private sealed record PreparedPermissionCheck(PermissionCode Permission, AccessScope Scope);
}

public sealed record AccessPermissionEvaluationRequest(IReadOnlyList<AccessPermissionCheck> Checks);

public sealed record AccessPermissionCheck(string Permission, string Scope);

public sealed record AccessPermissionEvaluationResponse(IReadOnlyList<AccessPermissionDecision> Permissions);

public sealed record AccessPermissionDecision(string Permission, string Scope, bool Allowed);
