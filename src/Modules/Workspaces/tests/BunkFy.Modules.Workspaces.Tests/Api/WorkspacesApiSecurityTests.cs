namespace BunkFy.Modules.Workspaces.Tests.Api;

using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Api;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.AccessControl;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Cqrs;
using Gma.Framework.Scoping;
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
}
