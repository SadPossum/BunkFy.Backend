namespace BunkFy.Modules.Staff.Api;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Api.Requests;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class StaffDataRightsEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/staff/data-rights-corrections")
            .WithModuleName(moduleName)
            .WithTags("Staff")
            .RequireAuthorization();

        group.MapPost("", async (
            StaffDataRightsCorrectionRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            StaffApiEndpointSupport.MarkSensitiveResponse(context);
            return (await dispatcher.SendAsync(
                new ApplyStaffDataRightsCorrectionCommand(
                    request.ExecutionId,
                    request.CaseId,
                    request.ApprovalRevision,
                    request.StaffMemberId,
                    request.ExpectedVersion,
                    request.DisplayName,
                    request.LegalName,
                    request.WorkEmail,
                    request.WorkPhone,
                    request.EmployeeNumber,
                    request.JobTitle,
                    request.Department,
                    StaffApiEndpointSupport.ResolveActor(context, subjectResolver)),
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes);
        })
            .Produces<StaffDataRightsCorrectionReceiptDto>()
            .RequireTenant()
            .RequireTenantPermission(DataRightsAdminPermissionCodes.Execute);
    }
}
