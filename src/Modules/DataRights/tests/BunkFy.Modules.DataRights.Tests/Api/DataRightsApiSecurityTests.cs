namespace BunkFy.Modules.DataRights.Tests.Api;

using BunkFy.Modules.DataRights.Api;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Cqrs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsApiSecurityTests
{
    [Fact]
    public async Task Case_endpoints_use_distinct_scoped_permissions()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();

        new DataRightsModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];
        const string cases = "/api/data-rights/properties/{propertyId:guid}/cases";
        AssertPermission(endpoints, HttpMethods.Get, cases, DataRightsAdminPermissionCodes.Read);
        AssertPermission(endpoints, HttpMethods.Post, cases, DataRightsAdminPermissionCodes.Create);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/discovery",
            DataRightsAdminPermissionCodes.Discover);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/review",
            DataRightsAdminPermissionCodes.Review);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/cancel",
            DataRightsAdminPermissionCodes.Manage);
    }

    private static void AssertPermission(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route,
        string expectedPermission)
    {
        RouteEndpoint endpoint = Assert.Single(endpoints, candidate =>
            string.Equals(
                candidate.RoutePattern.RawText?.Trim('/'),
                route.Trim('/'),
                StringComparison.Ordinal) &&
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                method,
                StringComparer.Ordinal) == true);
        AccessPermissionMetadata permission =
            Assert.Single(endpoint.Metadata.OfType<AccessPermissionMetadata>());
        Assert.Equal(expectedPermission, permission.Permission.Value);
        Assert.Equal(
            "data-rights-property",
            permission.ScopeResolverName);
    }
}
