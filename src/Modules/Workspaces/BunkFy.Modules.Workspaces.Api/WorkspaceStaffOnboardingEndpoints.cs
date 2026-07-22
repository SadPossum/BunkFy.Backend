namespace BunkFy.Modules.Workspaces.Api;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Api.Requests;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Queries;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Scoping;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class WorkspaceStaffOnboardingEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/workspace-staff-enrollment")
            .WithModuleName(moduleName)
            .WithTags("Workspace Staff Enrollment")
            .RequireAuthorization();

        group.MapPost("/applications", async (
            SubmitWorkspaceStaffOnboardingRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceStaffOnboardingSubmitter submitter,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? subject = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await submitter.SubmitAsync(
                new SubmitWorkspaceStaffOnboardingCommand(
                    request.SourceKind,
                    request.Token,
                    subject.Id,
                    request.DisplayName,
                    request.LegalName,
                    request.WorkEmail,
                    request.WorkPhone,
                    request.EmployeeNumber,
                    request.JobTitle,
                    request.Department),
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        }).Produces<WorkspaceStaffOnboardingDto>(StatusCodes.Status200OK);

        group.MapPost("/sources/invitations", async (
            IssueWorkspaceInvitationRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceStaffJoinSourceIssuer issuer,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? subject = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await issuer.IssueInvitationAsync(
                new WorkspaceInvitationIssuanceRequest(
                    request.SourceId,
                    request.RecipientEmail,
                    request.LifetimeHours,
                    request.ProfileKey,
                    request.PropertyIds ?? [],
                    subject.Id),
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(StaffAdminPermissionCodes.Manage)
            .Produces<WorkspaceStaffJoinSourceIssuanceDto>(StatusCodes.Status200OK);

        group.MapPost("/sources/enrollment-links", async (
            IssueWorkspaceEnrollmentLinkRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceStaffJoinSourceIssuer issuer,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? subject = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await issuer.IssueEnrollmentLinkAsync(
                new WorkspaceEnrollmentLinkIssuanceRequest(
                    request.SourceId,
                    request.LifetimeHours,
                    request.MaximumClaims,
                    request.ApprovalMode,
                    request.ProfileKey,
                    request.PropertyIds ?? [],
                    subject.Id),
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(StaffAdminPermissionCodes.Manage)
            .Produces<WorkspaceStaffJoinSourceIssuanceDto>(StatusCodes.Status200OK);

        group.MapGet("/sources", async (
            WorkspaceStaffOnboardingSourceKind sourceKind,
            int? page,
            int? pageSize,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceStaffJoinSourceManager manager,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? subject = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.ListAsync(
                sourceKind,
                page ?? PageRequest.DefaultPage,
                pageSize ?? PageRequest.DefaultPageSize,
                subject.Id,
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(StaffAdminPermissionCodes.Manage)
            .Produces<WorkspaceStaffJoinSourceListResponse>(StatusCodes.Status200OK);

        group.MapPost("/sources/invitations/{sourceId:guid}/revoke", async (
            Guid sourceId,
            ManageWorkspaceJoinSourceRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceStaffJoinSourceManager manager,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? subject = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.RevokeInvitationAsync(
                sourceId,
                request.ExpectedVersion,
                subject.Id,
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(StaffAdminPermissionCodes.Manage)
            .Produces<WorkspaceStaffJoinSourceDto>(StatusCodes.Status200OK);

        group.MapPost("/sources/enrollment-links/{sourceId:guid}/disable", async (
            Guid sourceId,
            ManageWorkspaceJoinSourceRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceStaffJoinSourceManager manager,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? subject = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.DisableEnrollmentLinkAsync(
                sourceId,
                request.ExpectedVersion,
                subject.Id,
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(StaffAdminPermissionCodes.Manage)
            .Produces<WorkspaceStaffJoinSourceDto>(StatusCodes.Status200OK);

        group.MapPost("/sources/invitations/{sourceId:guid}/replace", async (
            Guid sourceId,
            ReplaceWorkspaceJoinSourceRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceStaffJoinSourceReplacementManager manager,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? subject = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.ReplaceInvitationAsync(
                sourceId,
                request.ReplacementSourceId,
                request.ExpectedVersion,
                request.LifetimeHours,
                subject.Id,
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(StaffAdminPermissionCodes.Manage)
            .Produces<WorkspaceStaffJoinSourceReplacementDto>(StatusCodes.Status200OK);

        group.MapPost("/sources/enrollment-links/{sourceId:guid}/replace", async (
            Guid sourceId,
            ReplaceWorkspaceJoinSourceRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IWorkspaceStaffJoinSourceReplacementManager manager,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? subject = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await manager.ReplaceEnrollmentLinkAsync(
                sourceId,
                request.ReplacementSourceId,
                request.ExpectedVersion,
                request.LifetimeHours,
                subject.Id,
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(StaffAdminPermissionCodes.Manage)
            .Produces<WorkspaceStaffJoinSourceReplacementDto>(StatusCodes.Status200OK);

        group.MapGet("/{organizationId:guid}/applications/current", async (
            Guid organizationId,
            WorkspaceStaffOnboardingSourceKind sourceKind,
            Guid sourceId,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IScopeContextAccessor scopeContext,
            IRequestDispatcher dispatcher,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            AccessSubject? subject = WorkspacesApiEndpointSupport.ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            scopeContext.SetScope(organizationId.ToString("D"));
            return (await dispatcher.QueryAsync(
                new GetOwnWorkspaceStaffOnboardingQuery(sourceKind, sourceId, subject.Id),
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        }).Produces<WorkspaceStaffOnboardingDto>(StatusCodes.Status200OK);

        group.MapGet("/applications", async (
            int? page,
            int? pageSize,
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            return (await dispatcher.QueryAsync(
                new ListActionableWorkspaceStaffOnboardingQuery(
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(StaffAdminPermissionCodes.Manage)
            .Produces<WorkspaceStaffOnboardingListResponse>(StatusCodes.Status200OK);

        group.MapPost("/applications/{applicationId:guid}/retry", async (
            Guid applicationId,
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken token) =>
        {
            WorkspacesApiEndpointSupport.SetNoStore(context);
            return (await dispatcher.SendAsync(
                new RetryWorkspaceStaffOnboardingCommand(applicationId),
                token).ConfigureAwait(false)).ToHttpResult(
                    WorkspacesApiEndpointSupport.ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireTenantPermission(StaffAdminPermissionCodes.Manage)
            .Produces<WorkspaceStaffOnboardingDto>(StatusCodes.Status200OK);
    }
}
