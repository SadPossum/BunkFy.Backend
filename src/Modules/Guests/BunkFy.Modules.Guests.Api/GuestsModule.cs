namespace BunkFy.Modules.Guests.Api;

using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Api.Modules;
using Gma.Framework.Api.Observability;
using Gma.Framework.Api.Results;
using Gma.Framework.Api.Tenancy;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Tenancy.AccessControl.AspNetCore;
using BunkFy.Modules.Guests.Application;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Queries;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

public sealed class GuestsModule : IModule
{
    public string Name => GuestsModuleMetadata.Name;

    public void AddServices(IHostApplicationBuilder builder)
    {
        builder.SelectModuleProfile(GuestsProfiles.Default, "BunkFy.Modules.Guests.Api");
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IAccessHttpScopeResolver, GuestsPropertyAccessScopeResolver>());
        builder.Services.AddGuestsApplication();
        builder.AddGuestsPersistence();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints.MapGroup("/api/guests/properties/{propertyId:guid}")
            .WithModuleName(this.Name)
            .WithTags("Guests")
            .RequireAuthorization();

        group.MapGet("", async (
            Guid propertyId,
            string? search,
            GuestStatus? status,
            int? page,
            int? pageSize,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new ListGuestProfilesQuery(
                    propertyId,
                    search,
                    status,
                    page ?? PageRequest.DefaultPage,
                    pageSize ?? PageRequest.DefaultPageSize),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                GuestsAdminPermissionCodes.Read,
                GuestsPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{guestId:guid}", async (
            Guid propertyId,
            Guid guestId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetGuestProfileQuery(propertyId, guestId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                GuestsAdminPermissionCodes.Read,
                GuestsPropertyAccessScopeResolver.ResolverName);

        group.MapGet("/{guestId:guid}/stays", async (
            Guid propertyId,
            Guid guestId,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
            (await dispatcher.QueryAsync(
                new GetGuestStayHistoryQuery(propertyId, guestId),
                cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes))
            .RequireTenant()
            .RequireResolvedScopePermission(
                GuestsAdminPermissionCodes.Read,
                GuestsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("", async (
            Guid propertyId,
            GuestProfileWriteRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string? actor = ResolveActor(context, subjectResolver);
            return actor is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new CreateGuestProfileCommand(
                        propertyId,
                        request.DisplayName,
                        request.LegalName,
                        request.Email,
                        request.Phone,
                        request.DateOfBirth,
                        request.NationalityCountryCode,
                        request.PreferredLanguageTag,
                        request.Notes,
                        actor),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                GuestsAdminPermissionCodes.Create,
                GuestsPropertyAccessScopeResolver.ResolverName);

        group.MapPut("/{guestId:guid}", async (
            Guid propertyId,
            Guid guestId,
            GuestProfileUpdateRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string? actor = ResolveActor(context, subjectResolver);
            return actor is null
                ? Results.Unauthorized()
                : (await dispatcher.SendAsync(
                    new UpdateGuestProfileCommand(
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
                        actor),
                    cancellationToken).ConfigureAwait(false)).ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                GuestsAdminPermissionCodes.Manage,
                GuestsPropertyAccessScopeResolver.ResolverName);

        group.MapPost("/{guestId:guid}/archive", async (
            Guid propertyId,
            Guid guestId,
            ArchiveGuestProfileRequest request,
            HttpContext context,
            IAccessHttpSubjectResolver subjectResolver,
            IRequestDispatcher dispatcher,
            CancellationToken cancellationToken) =>
        {
            string? actor = ResolveActor(context, subjectResolver);
            Result<GuestProfileDto> result = !request.Confirmed
                ? Result.Failure<GuestProfileDto>(new("Guests.ConfirmationRequired", "Confirmation is required."))
                : actor is null
                    ? Result.Failure<GuestProfileDto>(new("Guests.AuthenticationRequired", "Authentication is required."))
                    : await dispatcher.SendAsync(
                        new ArchiveGuestProfileCommand(propertyId, guestId, request.ExpectedVersion, actor),
                        cancellationToken).ConfigureAwait(false);
            return actor is null ? Results.Unauthorized() : result.ToHttpResult(ErrorStatusCodes);
        })
            .RequireTenant()
            .RequireResolvedScopePermission(
                GuestsAdminPermissionCodes.Archive,
                GuestsPropertyAccessScopeResolver.ResolverName);
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

    private static string? ResolveActor(HttpContext context, IAccessHttpSubjectResolver subjectResolver)
    {
        AccessSubject? subject = subjectResolver.ResolveSubject(context);
        return subject is null ? null : $"{AccessSubjectKindNames.GetName(subject.Kind)}:{subject.Id}";
    }

    private static readonly ApiErrorStatusCodeMap ErrorStatusCodes = ApiErrorStatusCodeMap.Create(
        new(GuestsApplicationErrors.GuestNotFound.Code, StatusCodes.Status404NotFound),
        new(GuestsApplicationErrors.PropertyUnavailable.Code, StatusCodes.Status409Conflict),
        new(GuestsApplicationErrors.VersionConflict.Code, StatusCodes.Status409Conflict),
        new(GuestsApplicationErrors.GuestArchived.Code, StatusCodes.Status409Conflict),
        new(GuestsApplicationErrors.GuestAlreadyArchived.Code, StatusCodes.Status409Conflict),
        new("Guests.ConfirmationRequired", StatusCodes.Status400BadRequest));
}
