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

internal static class StaffPropertyAssignmentEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints, string moduleName)
    {
        RouteGroupBuilder members = endpoints.MapGroup("/api/staff/properties/{propertyId:guid}/members")
            .WithModuleName(moduleName).WithTags("Staff").RequireAuthorization();

        members.MapGet("", async (Guid propertyId, string? search, StaffStatus? status,
            int? page, int? pageSize, IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.QueryAsync(new ListStaffMembersAtPropertyQuery(propertyId, search, status,
                page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireResolvedScopePermission(StaffAdminPermissionCodes.Read,
                StaffPropertyAccessScopeResolver.ResolverName);
        members.MapGet("/{staffMemberId:guid}", async (Guid propertyId, Guid staffMemberId,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.QueryAsync(new GetStaffMemberAtPropertyQuery(propertyId, staffMemberId), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireResolvedScopePermission(StaffAdminPermissionCodes.Read,
                StaffPropertyAccessScopeResolver.ResolverName);
        members.MapPut("/{staffMemberId:guid}/assignment", async (Guid propertyId, Guid staffMemberId,
            StaffAssignmentRequest request, HttpContext context, IAccessHttpSubjectResolver subjects,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.SendAsync(new AssignStaffPropertyCommand(staffMemberId, propertyId,
                request.PropertyJobTitle, request.IsPrimary, request.EffectiveFrom,
                request.ExpectedVersion, StaffApiEndpointSupport.ResolveActor(context, subjects)), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireResolvedScopePermission(StaffAdminPermissionCodes.AssignProperties,
                StaffPropertyAccessScopeResolver.ResolverName);
        members.MapPost("/{staffMemberId:guid}/unassign", async (Guid propertyId, Guid staffMemberId,
            StaffUnassignmentRequest request, HttpContext context, IAccessHttpSubjectResolver subjects,
            IRequestDispatcher dispatcher, CancellationToken token) =>
            (await dispatcher.SendAsync(new UnassignStaffPropertyCommand(staffMemberId, propertyId,
                request.EffectiveTo, request.Reason, request.ExpectedVersion,
                StaffApiEndpointSupport.ResolveActor(context, subjects)), token)
                .ConfigureAwait(false)).ToHttpResult(StaffApiEndpointSupport.ErrorStatusCodes))
            .RequireTenant().RequireResolvedScopePermission(StaffAdminPermissionCodes.AssignProperties,
                StaffPropertyAccessScopeResolver.ResolverName);
    }
}
