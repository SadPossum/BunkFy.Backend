namespace BunkFy.Modules.Staff.Api;

using BunkFy.Modules.Staff.Api.Requests;
using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Security;
using Gma.Framework.Security.AspNetCore;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

internal static class StaffGovernanceEndpoints
{
    public static void Map(
        IEndpointRouteBuilder endpoints,
        string moduleName,
        StaffApiSecurityOptions security)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/staff/{staffMemberId:guid}")
            .WithModuleName(moduleName)
            .WithTags("Staff Governance")
            .RequireAuthorization();

        group.MapGet("/employment-governance", async (
            Guid staffMemberId,
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            StaffApiEndpointSupport.MarkSensitiveResponse(context);
            return (await dispatcher.QueryAsync(
                new GetStaffEmploymentGovernanceQuery(staffMemberId),
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(
                    StaffApiEndpointSupport.ErrorStatusCodes);
        })
            .Produces<StaffEmploymentGovernanceDto>(
                StatusCodes.Status200OK)
            .RequireTenant()
            .RequireTenantPermission(
                StaffAdminPermissionCodes
                    .EmploymentGovernanceManage)
            .RequireTenantPermission(
                StaffAdminPermissionCodes.SensitiveProfileRead);

        RouteHandlerBuilder configure =
            group.MapPut("/employment-governance", async (
                Guid staffMemberId,
                ConfigureStaffEmploymentGovernanceRequest request,
                HttpContext context,
                IAccessHttpSubjectResolver subjectResolver,
                IRequestDispatcher dispatcher,
                CancellationToken cancellationToken) =>
            {
                StaffApiEndpointSupport.MarkSensitiveResponse(context);
                return (await dispatcher.SendAsync(
                    new ConfigureStaffEmploymentGovernanceCommand(
                        request.IdempotencyKey,
                        staffMemberId,
                        request.ExpectedStaffVersion,
                        request.ExpectedGovernanceVersion,
                        request.OperatingCountryCode,
                        request.PolicyId,
                        request.PolicyVersion,
                        request.DataRegionId,
                        request.TransferProfileId,
                        request.RetentionPolicyId,
                        request.RetentionPolicyVersion,
                        request.AcceptedAcknowledgements,
                        StaffApiEndpointSupport.ResolveActor(
                            context,
                            subjectResolver)),
                    cancellationToken).ConfigureAwait(false))
                    .ToHttpResult(
                        StaffApiEndpointSupport.ErrorStatusCodes);
            })
                .Produces<
                    StaffEmploymentGovernanceChangeReceiptDto>(
                    StatusCodes.Status200OK)
                .RequireTenant()
                .RequireTenantPermission(
                    StaffAdminPermissionCodes
                        .EmploymentGovernanceManage)
                .RequireTenantPermission(
                    StaffAdminPermissionCodes
                        .SensitiveProfileRead);
        configure.RequireAssuranceWhenConfigured(
            security.EmploymentGovernanceAssurance);

        group.MapGet("/data-holds", async (
            Guid staffMemberId,
            StaffDataHoldStatus? status,
            int? page,
            int? pageSize,
            HttpContext context,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            StaffApiEndpointSupport.MarkSensitiveResponse(context);
            return (await dispatcher.QueryAsync(
                new ListStaffDataHoldsQuery(
                    staffMemberId,
                    status,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(
                    StaffApiEndpointSupport.ErrorStatusCodes);
        })
            .Produces<StaffDataHoldListResponse>(
                StatusCodes.Status200OK)
            .RequireTenant()
            .RequireTenantPermission(
                StaffAdminPermissionCodes.DataHoldsManage)
            .RequireTenantPermission(
                StaffAdminPermissionCodes.SensitiveProfileRead);

        group.MapPost("/data-holds", async (
            Guid staffMemberId,
            PlaceStaffDataHoldRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            StaffApiEndpointSupport.MarkSensitiveResponse(context);
            return (await dispatcher.SendAsync(
                new PlaceStaffDataHoldCommand(
                    request.IdempotencyKey,
                    staffMemberId,
                    request.ExpectedStaffVersion,
                    request.ReasonCode,
                    StaffApiEndpointSupport.ResolveActor(
                        context,
                        subjectResolver)),
                cancellationToken).ConfigureAwait(false))
                .ToHttpResult(
                    StaffApiEndpointSupport.ErrorStatusCodes);
        })
            .Produces<StaffDataHoldReceiptDto>(
                StatusCodes.Status200OK)
            .RequireTenant()
            .RequireTenantPermission(
                StaffAdminPermissionCodes.DataHoldsManage)
            .RequireTenantPermission(
                StaffAdminPermissionCodes.SensitiveProfileRead);

        RouteHandlerBuilder release =
            group.MapPost(
                "/data-holds/{holdId:guid}/release",
                async (
                    Guid staffMemberId,
                    Guid holdId,
                    ReleaseStaffDataHoldRequest request,
                    HttpContext context,
                    IAccessHttpSubjectResolver subjectResolver,
                    IRequestDispatcher dispatcher,
                    CancellationToken cancellationToken) =>
                {
                    StaffApiEndpointSupport.MarkSensitiveResponse(
                        context);
                    Result<StaffDataHoldReceiptDto> result =
                        !request.Confirmed
                            ? Result.Failure<
                                StaffDataHoldReceiptDto>(
                                StaffApplicationErrors
                                    .ConfirmationRequired)
                            : await dispatcher.SendAsync(
                                new ReleaseStaffDataHoldCommand(
                                    request.IdempotencyKey,
                                    staffMemberId,
                                    holdId,
                                    request.ExpectedStaffVersion,
                                    request.ExpectedHoldVersion,
                                    StaffApiEndpointSupport.ResolveActor(
                                        context,
                                        subjectResolver)),
                                cancellationToken)
                                .ConfigureAwait(false);
                    return result.ToHttpResult(
                        StaffApiEndpointSupport.ErrorStatusCodes);
                })
                .Produces<StaffDataHoldReceiptDto>(
                    StatusCodes.Status200OK)
                .RequireTenant()
                .RequireTenantPermission(
                    StaffAdminPermissionCodes.DataHoldsManage)
                .RequireTenantPermission(
                    StaffAdminPermissionCodes
                        .SensitiveProfileRead);
        release.RequireAssuranceWhenConfigured(
            security.DataHoldReleaseAssurance);
    }

    private static RouteHandlerBuilder RequireAssuranceWhenConfigured(
        this RouteHandlerBuilder endpoint,
        AuthenticationAssuranceRequirement? requirement) =>
        requirement is null
            ? endpoint
            : endpoint.RequireAuthenticationAssurance(requirement);
}
