namespace BunkFy.Modules.Staff.Api;

using BunkFy.Modules.Staff.Api.Requests;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class StaffSelfServiceEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder self = endpoints.MapGroup("/api/staff/me")
            .WithModuleName(moduleName)
            .WithTags("Staff")
            .RequireAuthorization();

        self.MapGet("", async (
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IRequestDispatcher dispatcher,
            CancellationToken token) =>
        {
            StaffApiEndpointSupport.MarkSensitiveResponse(context);
            AccessSubject? subject = ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.QueryAsync(
                new GetCurrentStaffMemberQuery(subject.Id),
                token).ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes);
        }).Produces<StaffMemberDto>(StatusCodes.Status200OK).RequireTenant();

        self.MapPut("", async (
            StaffProfileUpdateRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjects,
            IRequestDispatcher dispatcher,
            CancellationToken token) =>
        {
            StaffApiEndpointSupport.MarkSensitiveResponse(context);
            AccessSubject? subject = ResolveUser(context, subjects);
            if (subject is null)
            {
                return Results.Unauthorized();
            }

            return (await dispatcher.SendAsync(
                new UpdateCurrentStaffMemberCommand(
                    subject.Id,
                    request.DisplayName,
                    request.LegalName,
                    request.WorkEmail,
                    request.WorkPhone,
                    request.EmployeeNumber,
                    request.JobTitle,
                    request.Department,
                    request.ExpectedVersion,
                    $"{AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}"),
                token).ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes);
        }).Produces<StaffMemberDto>(StatusCodes.Status200OK).RequireTenant();
    }

    private static AccessSubject? ResolveUser(HttpContext context, IAccessHttpSubjectResolver resolver)
    {
        AccessSubject? subject = resolver.ResolveSubject(context);
        return subject?.Kind == AccessSubjectKind.User ? subject : null;
    }
}
