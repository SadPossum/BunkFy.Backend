namespace BunkFy.Modules.Workspaces.Api;

using BunkFy.Modules.Workspaces.Api.Requests;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Results;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Gma.Modules.AccessControl.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class WorkspaceAccessManagementEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/workspace-access")
            .WithModuleName(moduleName)
            .WithTags("Workspace Access")
            .RequireAuthorization();

        group.MapGet("/catalogue", async (
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceAccessProfileManager manager,
            CancellationToken cancellationToken) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? actor = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.GetCatalogueAsync(actor, cancellationToken).ConfigureAwait(false))
                .ToHttpResult(WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(AccessControlProfilePermissionCodes.Read)
            .Produces<WorkspaceAccessCatalogueDto>(StatusCodes.Status200OK);

        group.MapGet("/profiles", async (
            bool? includeArchived,
            int? page,
            int? pageSize,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceAccessProfileManager manager,
            CancellationToken cancellationToken) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? actor = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.ListProfilesAsync(
                includeArchived ?? false,
                page ?? 1,
                pageSize ?? 25,
                actor,
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(AccessControlProfilePermissionCodes.Read)
            .Produces<WorkspaceAccessProfileListResponse>(StatusCodes.Status200OK);

        group.MapPost("/profiles", async (
            CreateWorkspaceAccessProfileRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceAccessProfileManager manager,
            CancellationToken cancellationToken) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? actor = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.CreateProfileAsync(
                new WorkspaceAccessProfileCreation(
                    request.RequestId,
                    request.DisplayName,
                    request.Description,
                    request.Permissions),
                actor,
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(AccessControlProfilePermissionCodes.Manage)
            .Produces<WorkspaceAccessProfileDto>(StatusCodes.Status200OK);

        group.MapPut("/profiles/{profileId:guid}", async (
            Guid profileId,
            UpdateWorkspaceAccessProfileRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceAccessProfileManager manager,
            CancellationToken cancellationToken) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? actor = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.UpdateProfileAsync(
                profileId,
                new WorkspaceAccessProfileUpdate(
                    request.DisplayName,
                    request.Description,
                    request.Permissions,
                    request.ExpectedVersion),
                actor,
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(AccessControlProfilePermissionCodes.Manage)
            .Produces<WorkspaceAccessProfileDto>(StatusCodes.Status200OK);

        group.MapPost("/profiles/{profileId:guid}/archive", async (
            Guid profileId,
            ArchiveWorkspaceAccessProfileRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceAccessProfileManager manager,
            CancellationToken cancellationToken) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? actor = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            Result result = await manager.ArchiveProfileAsync(
                profileId,
                request.ExpectedVersion,
                actor,
                cancellationToken).ConfigureAwait(false);
            return result.IsSuccess
                ? Results.NoContent()
                : result.ToHttpResult(WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(AccessControlProfilePermissionCodes.Manage)
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/members/{subjectId}/access", async (
            string subjectId,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceMemberAccessManager manager,
            CancellationToken cancellationToken) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? actor = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.GetAsync(
                subjectId,
                actor,
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(AccessControlProfilePermissionCodes.Read)
            .Produces<WorkspaceMemberAccessDto>(StatusCodes.Status200OK);

        group.MapPut("/members/{subjectId}/access", async (
            string subjectId,
            UpdateWorkspaceMemberAccessRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceMemberAccessManager manager,
            CancellationToken cancellationToken) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? actor = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (actor is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.UpdateAsync(
                subjectId,
                new WorkspaceMemberAccessUpdate(request.ProfileId, request.PropertyIds),
                actor,
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(AccessControlProfilePermissionCodes.Assign)
            .Produces<WorkspaceMemberAccessDto>(StatusCodes.Status200OK);
    }
}
