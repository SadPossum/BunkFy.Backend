namespace BunkFy.Modules.Workspaces.Api;

using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Results;
using Microsoft.AspNetCore.Http;

internal static class WorkspacesApiEndpointSupport
{
    public static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(WorkspaceStaffOnboardingApplicationErrors.ScopeRequired.Code, StatusCodes.Status400BadRequest),
        new(WorkspaceStaffOnboardingApplicationErrors.VerifiedIdentityRequired.Code, StatusCodes.Status403Forbidden),
        new(WorkspaceStaffOnboardingApplicationErrors.JoinTokenInvalid.Code, StatusCodes.Status404NotFound),
        new(WorkspaceStaffOnboardingApplicationErrors.ApplicationNotFound.Code, StatusCodes.Status404NotFound),
        new(WorkspaceStaffOnboardingApplicationErrors.ProvisioningFailed.Code, StatusCodes.Status409Conflict),
        new(WorkspaceStaffOnboardingErrors.Invalid.Code, StatusCodes.Status400BadRequest),
        new(WorkspaceStaffOnboardingErrors.Unavailable.Code, StatusCodes.Status409Conflict),
        new(WorkspaceStaffOnboardingErrors.ClaimConflict.Code, StatusCodes.Status409Conflict),
        new(WorkspaceStaffOnboardingErrors.StateConflict.Code, StatusCodes.Status409Conflict));

    public static AccessSubject? ResolveUser(
        HttpContext context,
        IAccessHttpSubjectResolver resolver)
    {
        AccessSubject? subject = resolver.ResolveSubject(context);
        return subject?.Kind == AccessSubjectKind.User ? subject : null;
    }

    public static void SetNoStore(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
    }
}
