namespace BunkFy.Modules.Guests.AdminApi;

using System.Security.Claims;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Api;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using BunkFy.Modules.Guests.Admin.Contracts;
using BunkFy.Modules.Guests.Application;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Queries;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;

public sealed class GuestsAdminApiModule : IAdminApiModule
{
    public string Name => GuestsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(GuestsProfiles.Default, "BunkFy.Modules.Guests.AdminApi");
        builder.Services.AddGuestsApplication();
        builder.AddGuestsPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/admin/guests/properties/{propertyId:guid}")
            .WithModuleName(this.Name)
            .WithTags("Guests Admin")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid propertyId,
            string? search,
            GuestStatus? status,
            int? page,
            int? pageSize,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(GuestsAdminOperationNames.List, GuestsAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(new ListGuestProfilesQuery(
                    propertyId,
                    search,
                    status,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize), token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapGet("/{guestId:guid}", async (
            Guid propertyId,
            Guid guestId,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(GuestsAdminOperationNames.Get, GuestsAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetGuestProfileQuery(propertyId, guestId), token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapGet("/{guestId:guid}/stays", async (
            Guid propertyId,
            Guid guestId,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(GuestsAdminOperationNames.StayHistory, GuestsAdminPermissions.Read),
                requireTenant: true,
                token => dispatcher.QueryAsync(new GetGuestStayHistoryQuery(propertyId, guestId), token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPost("", async (
            Guid propertyId,
            GuestProfileWriteRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(GuestsAdminOperationNames.Create, GuestsAdminPermissions.Create),
                requireTenant: true,
                token => dispatcher.SendAsync(new CreateGuestProfileCommand(
                    propertyId,
                    request.DisplayName,
                    request.LegalName,
                    request.Email,
                    request.Phone,
                    request.DateOfBirth,
                    request.NationalityCountryCode,
                    request.PreferredLanguageTag,
                    request.Notes,
                    Actor(context)), token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPut("/{guestId:guid}", async (
            Guid propertyId,
            Guid guestId,
            GuestProfileUpdateRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(GuestsAdminOperationNames.Update, GuestsAdminPermissions.Manage),
                requireTenant: true,
                token => dispatcher.SendAsync(new UpdateGuestProfileCommand(
                    propertyId,
                    guestId,
                    request.DisplayName,
                    request.LegalName,
                    request.Email,
                    request.Phone,
                    request.DateOfBirth,
                    request.NationalityCountryCode,
                    request.PreferredLanguageTag,
                    request.Notes,
                    request.ExpectedVersion,
                    Actor(context)), token),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));

        group.MapPost("/{guestId:guid}/archive", async (
            Guid propertyId,
            Guid guestId,
            ArchiveGuestProfileRequest request,
            HttpContext context,
            AdminApiExecutor executor,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) => await executor.ExecuteAsync(
                context,
                AdminOperation.Create(GuestsAdminOperationNames.Archive, GuestsAdminPermissions.Archive),
                requireTenant: true,
                token => request.Confirmed
                    ? dispatcher.SendAsync(new ArchiveGuestProfileCommand(
                        propertyId, guestId, request.ExpectedVersion, Actor(context)), token)
                    : Task.FromResult(Result.Failure<GuestProfileDto>(AdminErrors.ConfirmationRequired)),
                cancellationToken,
                errorStatusCodes: ErrorStatusCodes).ConfigureAwait(false));
    }

    public sealed record GuestProfileWriteRequest(
        string DisplayName,
        string? LegalName,
        string? Email,
        string? Phone,
        DateOnly? DateOfBirth,
        string? NationalityCountryCode,
        string? PreferredLanguageTag,
        string? Notes);

    public sealed record GuestProfileUpdateRequest(
        string DisplayName,
        string? LegalName,
        string? Email,
        string? Phone,
        DateOnly? DateOfBirth,
        string? NationalityCountryCode,
        string? PreferredLanguageTag,
        string? Notes,
        long ExpectedVersion);

    public sealed record ArchiveGuestProfileRequest(long ExpectedVersion, bool Confirmed);

    private static string Actor(HttpContext context)
    {
        string identity = context.User.FindFirst("sub")?.Value
            ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.Identity?.Name
            ?? $"authenticated:{context.User.Identity?.AuthenticationType ?? "unknown"}";
        return $"admin-api:{identity}";
    }

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(GuestsApplicationErrors.GuestNotFound.Code, StatusCodes.Status404NotFound),
        new(GuestsApplicationErrors.PropertyUnavailable.Code, StatusCodes.Status409Conflict),
        new(GuestsApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(GuestsApplicationErrors.GuestArchived.Code, StatusCodes.Status409Conflict),
        new(GuestsApplicationErrors.GuestAlreadyArchived.Code, StatusCodes.Status409Conflict));
}
