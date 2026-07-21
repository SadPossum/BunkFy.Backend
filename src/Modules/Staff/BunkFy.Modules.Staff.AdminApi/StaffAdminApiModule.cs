namespace BunkFy.Modules.Staff.AdminApi;

using System.Security.Claims;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using BunkFy.Modules.Staff.Admin.Contracts;
using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Queries;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Persistence;

public sealed class StaffAdminApiModule : IAdminApiModule
{
    public string Name => StaffModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(StaffProfiles.Default, "BunkFy.Modules.Staff.AdminApi");
        builder.Services.AddStaffApplication();
        builder.AddStaffPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/admin/staff/members")
            .WithModuleName(this.Name).WithTags("Staff Admin").RequireAuthorization();
        group.MapGet("", async (string? search, StaffStatus? status, int? page, int? pageSize,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher,
            CancellationToken token) => await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.List, StaffAdminPermissions.Read), true,
                ct => dispatcher.QueryAsync(new ListStaffMembersQuery(search, status,
                    page ?? PageRequest.DefaultPage, pageSize ?? PageRequest.DefaultPageSize), ct),
                token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapGet("/{staffMemberId:guid}", async (Guid staffMemberId, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            MarkSensitiveResponse(context);
            return await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.Get, StaffAdminPermissions.SensitiveProfileRead), true,
                ct => dispatcher.QueryAsync(new GetStaffMemberQuery(staffMemberId), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false);
        });
        group.MapPost("", async (StaffProfileWriteRequest request, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            MarkSensitiveResponse(context);
            return await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.Create, StaffAdminPermissions.Create), true,
                ct => dispatcher.SendAsync(new CreateStaffMemberCommand(request.DisplayName,
                    request.LegalName, request.WorkEmail, request.WorkPhone, request.EmployeeNumber,
                    request.JobTitle, request.Department, request.AuthSubjectId, Actor(context)), ct),
                token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false);
        });
        group.MapPut("/{staffMemberId:guid}", async (Guid staffMemberId, StaffProfileUpdateRequest request,
            HttpContext context, AdminApiExecutor executor, IRequestDispatcher dispatcher,
            CancellationToken token) =>
        {
            MarkSensitiveResponse(context);
            return await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.Update, StaffAdminPermissions.Manage), true,
                ct => dispatcher.SendAsync(new UpdateStaffMemberCommand(staffMemberId,
                    request.DisplayName, request.LegalName, request.WorkEmail, request.WorkPhone,
                    request.EmployeeNumber, request.JobTitle, request.Department, request.ExpectedVersion,
                    Actor(context)), ct), token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false);
        });
        group.MapPut("/{staffMemberId:guid}/auth-subject", async (Guid staffMemberId,
            StaffAuthSubjectRequest request, HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) =>
        {
            MarkSensitiveResponse(context);
            return await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.SetAuthSubject, StaffAdminPermissions.Manage), true,
                ct => request.Confirmed ? dispatcher.SendAsync(new SetStaffAuthSubjectCommand(staffMemberId,
                    request.AuthSubjectId, request.ExpectedVersion, Actor(context)), ct)
                    : Task.FromResult(Result.Failure<StaffMemberDto>(AdminErrors.ConfirmationRequired)),
                token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false);
        });
        group.MapPost("/{staffMemberId:guid}/suspend", async (Guid staffMemberId,
            StaffLifecycleRequest request, HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) => await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.Suspend, StaffAdminPermissions.ManageLifecycle), true,
                ct => dispatcher.SendAsync(new SuspendStaffMemberCommand(staffMemberId, request.Reason,
                    request.ExpectedVersion, Actor(context)), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{staffMemberId:guid}/resume", async (Guid staffMemberId,
            StaffLifecycleRequest request, HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) => await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.Resume, StaffAdminPermissions.ManageLifecycle), true,
                ct => dispatcher.SendAsync(new ResumeStaffMemberCommand(staffMemberId, request.Reason,
                    request.ExpectedVersion, Actor(context)), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{staffMemberId:guid}/depart", async (Guid staffMemberId,
            StaffDepartureRequest request, HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) => await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.Depart, StaffAdminPermissions.ManageLifecycle), true,
                ct => request.Confirmed ? dispatcher.SendAsync(new DepartStaffMemberCommand(staffMemberId,
                    request.EffectiveOn, request.Reason, request.ExpectedVersion, Actor(context)), ct)
                    : Task.FromResult(Result.Failure<StaffDirectoryMemberDto>(AdminErrors.ConfirmationRequired)),
                token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPut("/{staffMemberId:guid}/properties/{propertyId:guid}", async (Guid staffMemberId,
            Guid propertyId, StaffAssignmentRequest request, HttpContext context, AdminApiExecutor executor,
            IRequestDispatcher dispatcher, CancellationToken token) => await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.AssignProperty,
                    StaffAdminPermissions.AssignProperties), true,
                ct => dispatcher.SendAsync(new AssignStaffPropertyCommand(staffMemberId, propertyId,
                    request.PropertyJobTitle, request.IsPrimary, request.EffectiveFrom,
                    request.ExpectedVersion, Actor(context)), ct), token,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
        group.MapPost("/{staffMemberId:guid}/properties/{propertyId:guid}/unassign", async (
            Guid staffMemberId, Guid propertyId, StaffUnassignmentRequest request, HttpContext context,
            AdminApiExecutor executor, IRequestDispatcher dispatcher, CancellationToken token) =>
            await executor.ExecuteAsync(context,
                AdminOperation.Create(StaffAdminOperationNames.UnassignProperty,
                    StaffAdminPermissions.AssignProperties), true,
                ct => dispatcher.SendAsync(new UnassignStaffPropertyCommand(staffMemberId, propertyId,
                    request.EffectiveTo, request.Reason, request.ExpectedVersion, Actor(context)), ct),
                token, errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    public sealed record StaffProfileWriteRequest(string DisplayName, string? LegalName,
        string? WorkEmail, string? WorkPhone, string? EmployeeNumber, string? JobTitle,
        string? Department, string? AuthSubjectId);
    public sealed record StaffProfileUpdateRequest(string DisplayName, string? LegalName,
        string? WorkEmail, string? WorkPhone, string? EmployeeNumber, string? JobTitle,
        string? Department, long ExpectedVersion);
    public sealed record StaffAuthSubjectRequest(string? AuthSubjectId, long ExpectedVersion, bool Confirmed);
    public sealed record StaffLifecycleRequest(string Reason, long ExpectedVersion);
    public sealed record StaffDepartureRequest(DateOnly EffectiveOn, string Reason,
        long ExpectedVersion, bool Confirmed);
    public sealed record StaffAssignmentRequest(string? PropertyJobTitle, bool IsPrimary,
        DateOnly EffectiveFrom, long ExpectedVersion);
    public sealed record StaffUnassignmentRequest(DateOnly EffectiveTo, string Reason, long ExpectedVersion);

    private static string Actor(HttpContext context)
    {
        string identity = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.Identity?.Name
            ?? $"authenticated:{context.User.Identity?.AuthenticationType ?? "unknown"}";
        return $"admin-api:{identity}";
    }

    private static void MarkSensitiveResponse(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(StaffApplicationErrors.StaffMemberNotFound.Code, StatusCodes.Status404NotFound),
        new(StaffApplicationErrors.PropertyUnavailable.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.EmployeeNumberConflict.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.AuthSubjectConflict.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.StaffSuspended.Code, StatusCodes.Status409Conflict),
        new(StaffApplicationErrors.StaffDeparted.Code, StatusCodes.Status409Conflict));
}
