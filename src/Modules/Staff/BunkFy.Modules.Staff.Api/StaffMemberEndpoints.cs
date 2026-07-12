namespace BunkFy.Modules.Staff.Api;

using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using BunkFy.Modules.Staff.Api.Requests;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;

internal static class StaffMemberEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder members = endpoints.MapGroup("/api/staff/members")
            .WithModuleName(moduleName).WithTags("Staff").RequireAuthorization();

        members.MapGet("", async (string? search, StaffStatus? status, int? page, int? pageSize,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.QueryAsync(new ListStaffMembersQuery(search, status,
                page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireTenantPermission(StaffAdminPermissionCodes.Read);
        members.MapGet("/{staffMemberId:guid}", async (Guid staffMemberId,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.QueryAsync(new GetStaffMemberQuery(staffMemberId), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireTenantPermission(StaffAdminPermissionCodes.Read);
        members.MapPost("", async (StaffProfileWriteRequest request, HttpContext context,
            IAccessHttpSubjectResolver subjects, IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.SendAsync(new CreateStaffMemberCommand(request.DisplayName, request.LegalName,
                request.WorkEmail, request.WorkPhone, request.EmployeeNumber, request.JobTitle,
                request.Department, request.AuthSubjectId, StaffApiEndpointSupport.ResolveActor(context, subjects)), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireTenantPermission(StaffAdminPermissionCodes.Create);
        members.MapPut("/{staffMemberId:guid}", async (Guid staffMemberId,
            StaffProfileUpdateRequest request, HttpContext context, IAccessHttpSubjectResolver subjects,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.SendAsync(new UpdateStaffMemberCommand(staffMemberId, request.DisplayName,
                request.LegalName, request.WorkEmail, request.WorkPhone, request.EmployeeNumber,
                request.JobTitle, request.Department, request.ExpectedVersion,
                StaffApiEndpointSupport.ResolveActor(context, subjects)), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireTenantPermission(StaffAdminPermissionCodes.Manage);
        members.MapPut("/{staffMemberId:guid}/auth-subject", async (Guid staffMemberId,
            StaffAuthSubjectRequest request, HttpContext context, IAccessHttpSubjectResolver subjects,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.SendAsync(new SetStaffAuthSubjectCommand(staffMemberId, request.AuthSubjectId,
                request.ExpectedVersion, StaffApiEndpointSupport.ResolveActor(context, subjects)), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireTenantPermission(StaffAdminPermissionCodes.Manage);
        members.MapPost("/{staffMemberId:guid}/suspend", async (Guid staffMemberId,
            StaffLifecycleRequest request, HttpContext context, IAccessHttpSubjectResolver subjects,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.SendAsync(new SuspendStaffMemberCommand(staffMemberId, request.Reason,
                request.ExpectedVersion, StaffApiEndpointSupport.ResolveActor(context, subjects)), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireTenantPermission(StaffAdminPermissionCodes.ManageLifecycle);
        members.MapPost("/{staffMemberId:guid}/resume", async (Guid staffMemberId,
            StaffLifecycleRequest request, HttpContext context, IAccessHttpSubjectResolver subjects,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.SendAsync(new ResumeStaffMemberCommand(staffMemberId, request.Reason,
                request.ExpectedVersion, StaffApiEndpointSupport.ResolveActor(context, subjects)), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireTenantPermission(StaffAdminPermissionCodes.ManageLifecycle);
        members.MapPost("/{staffMemberId:guid}/depart", async (Guid staffMemberId,
            StaffDepartureRequest request, HttpContext context, IAccessHttpSubjectResolver subjects,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.SendAsync(new DepartStaffMemberCommand(staffMemberId, request.EffectiveOn,
                request.Reason, request.ExpectedVersion, StaffApiEndpointSupport.ResolveActor(context, subjects)), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireTenantPermission(StaffAdminPermissionCodes.ManageLifecycle);
    }
}
