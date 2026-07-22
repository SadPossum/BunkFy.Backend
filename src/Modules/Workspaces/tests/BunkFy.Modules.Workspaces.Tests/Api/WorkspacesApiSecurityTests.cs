namespace BunkFy.Modules.Workspaces.Tests.Api;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Api;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Cqrs;
using Gma.Framework.Scoping;
using Gma.Modules.AccessControl.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class WorkspacesApiSecurityTests
{
    [Fact]
    public async Task Join_source_issuance_requires_staff_management_and_returns_the_one_time_result()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceStaffJoinSourceIssuer>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceStaffJoinSourceManager>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceStaffJoinSourceReplacementManager>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceAccessProfileManager>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceMemberAccessManager>(_ => null!);
        builder.Services.AddSingleton<IScopeContextAccessor>(_ => null!);
        await using WebApplication app = builder.Build();
        new WorkspacesModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];

        AssertProtectedIssuanceRoute(
            endpoints,
            "/api/workspace-staff-enrollment/sources/invitations");
        AssertProtectedIssuanceRoute(
            endpoints,
            "/api/workspace-staff-enrollment/sources/enrollment-links");
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-staff-enrollment/sources",
            HttpMethods.Get,
            StaffAdminPermissionCodes.Manage,
            typeof(WorkspaceStaffJoinSourceListResponse),
            StatusCodes.Status200OK);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-staff-enrollment/sources/invitations/{sourceId:guid}/revoke",
            HttpMethods.Post,
            StaffAdminPermissionCodes.Manage,
            typeof(WorkspaceStaffJoinSourceDto),
            StatusCodes.Status200OK);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-staff-enrollment/sources/enrollment-links/{sourceId:guid}/disable",
            HttpMethods.Post,
            StaffAdminPermissionCodes.Manage,
            typeof(WorkspaceStaffJoinSourceDto),
            StatusCodes.Status200OK);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-staff-enrollment/sources/invitations/{sourceId:guid}/replace",
            HttpMethods.Post,
            StaffAdminPermissionCodes.Manage,
            typeof(WorkspaceStaffJoinSourceReplacementDto),
            StatusCodes.Status200OK);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-staff-enrollment/sources/enrollment-links/{sourceId:guid}/replace",
            HttpMethods.Post,
            StaffAdminPermissionCodes.Manage,
            typeof(WorkspaceStaffJoinSourceReplacementDto),
            StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Workspace_access_routes_use_read_and_manage_permissions()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceStaffJoinSourceIssuer>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceStaffJoinSourceManager>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceStaffJoinSourceReplacementManager>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceAccessProfileManager>(_ => null!);
        builder.Services.AddSingleton<IWorkspaceMemberAccessManager>(_ => null!);
        builder.Services.AddSingleton<IScopeContextAccessor>(_ => null!);
        await using WebApplication app = builder.Build();
        new WorkspacesModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];

        AssertProtectedRoute(
            endpoints,
            "/api/workspace-access/catalogue",
            HttpMethods.Get,
            AccessControlProfilePermissionCodes.Read,
            typeof(WorkspaceAccessCatalogueDto),
            StatusCodes.Status200OK);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-access/profiles",
            HttpMethods.Get,
            AccessControlProfilePermissionCodes.Read,
            typeof(WorkspaceAccessProfileListResponse),
            StatusCodes.Status200OK);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-access/profiles",
            HttpMethods.Post,
            AccessControlProfilePermissionCodes.Manage,
            typeof(WorkspaceAccessProfileDto),
            StatusCodes.Status200OK);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-access/profiles/{profileId:guid}",
            HttpMethods.Put,
            AccessControlProfilePermissionCodes.Manage,
            typeof(WorkspaceAccessProfileDto),
            StatusCodes.Status200OK);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-access/profiles/{profileId:guid}/archive",
            HttpMethods.Post,
            AccessControlProfilePermissionCodes.Manage,
            type: typeof(void),
            statusCode: StatusCodes.Status204NoContent);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-access/members/{subjectId}/access",
            HttpMethods.Get,
            AccessControlProfilePermissionCodes.Read,
            typeof(WorkspaceMemberAccessDto),
            StatusCodes.Status200OK);
        AssertProtectedRoute(
            endpoints,
            "/api/workspace-access/members/{subjectId}/access",
            HttpMethods.Put,
            AccessControlProfilePermissionCodes.Assign,
            typeof(WorkspaceMemberAccessDto),
            StatusCodes.Status200OK);
    }

    private static void AssertProtectedIssuanceRoute(
        IEnumerable<RouteEndpoint> endpoints,
        string route)
    {
        RouteEndpoint endpoint = Assert.Single(endpoints, candidate =>
            string.Equals(
                candidate.RoutePattern.RawText?.Trim('/'),
                route.Trim('/'),
                StringComparison.Ordinal) &&
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                HttpMethods.Post,
                StringComparer.Ordinal) == true);
        AccessPermissionMetadata permission = Assert.Single(
            endpoint.Metadata.OfType<AccessPermissionMetadata>());
        IProducesResponseTypeMetadata response = Assert.Single(
            endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == StatusCodes.Status200OK);

        Assert.Equal(StaffAdminPermissionCodes.Manage, permission.Permission.Value);
        Assert.Equal("tenant", permission.ScopeResolverName);
        Assert.Equal(typeof(WorkspaceStaffJoinSourceIssuanceDto), response.Type);
    }

    private static void AssertProtectedRoute(
        IEnumerable<RouteEndpoint> endpoints,
        string route,
        string method,
        string permissionCode,
        Type? type,
        int statusCode)
    {
        RouteEndpoint endpoint = Assert.Single(endpoints, candidate =>
            string.Equals(
                candidate.RoutePattern.RawText?.Trim('/'),
                route.Trim('/'),
                StringComparison.Ordinal) &&
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                method,
                StringComparer.Ordinal) == true);
        AccessPermissionMetadata permission = Assert.Single(
            endpoint.Metadata.OfType<AccessPermissionMetadata>());
        IProducesResponseTypeMetadata response = Assert.Single(
            endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == statusCode);

        Assert.Equal(permissionCode, permission.Permission.Value);
        Assert.Equal("tenant", permission.ScopeResolverName);
        Assert.Equal(type, response.Type);
    }
}
