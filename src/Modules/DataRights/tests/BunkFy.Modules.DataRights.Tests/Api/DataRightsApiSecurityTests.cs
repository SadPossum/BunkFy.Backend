namespace BunkFy.Modules.DataRights.Tests.Api;

using BunkFy.Modules.DataRights.Api;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Cqrs;
using Gma.Framework.Security;
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
        builder.Services.Configure<DataRightsApiSecurityOptions>(options =>
            options.AnonymisationExecutionAssurance = new AuthenticationAssuranceRequirement(
                ["urn:test:acr:mfa"],
                TimeSpan.FromMinutes(10)));
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
            HttpMethods.Get,
            $"{cases}/{{caseId:guid}}/subjects",
            DataRightsAdminPermissionCodes.Discover);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/subjects/discover",
            DataRightsAdminPermissionCodes.Discover);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/subjects/select",
            DataRightsAdminPermissionCodes.Discover);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/subjects/unselect",
            DataRightsAdminPermissionCodes.Discover);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/review",
            DataRightsAdminPermissionCodes.Review);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/decision",
            DataRightsAdminPermissionCodes.Decide);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/decision/outcome",
            DataRightsAdminPermissionCodes.Decide);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/cancel",
            DataRightsAdminPermissionCodes.Manage);
        AssertPermission(
            endpoints,
            HttpMethods.Get,
            $"{cases}/{{caseId:guid}}/execution",
            DataRightsAdminPermissionCodes.Read);
        AssertPermissionSet(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/execution",
            (DataRightsAdminPermissionCodes.Erase, "tenant"),
            (DataRightsAdminPermissionCodes.Read, "data-rights-property"));
        AssertAssurance(
            endpoints,
            HttpMethods.Post,
            $"{cases}/{{caseId:guid}}/execution");
    }

    [Fact]
    public void Sensitive_discovery_responses_are_not_cacheable()
    {
        DefaultHttpContext context = new();

        DataRightsSensitiveResponseHeaders.Apply(context.Response);

        Assert.Equal("no-store, no-cache, max-age=0", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        Assert.Equal("0", context.Response.Headers.Expires);
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

    private static void AssertPermissionSet(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route,
        params (string Permission, string ScopeResolver)[] expected)
    {
        RouteEndpoint endpoint = FindEndpoint(endpoints, method, route);
        AccessPermissionSetMetadata permissionSet =
            Assert.Single(endpoint.Metadata.OfType<AccessPermissionSetMetadata>());
        Assert.Equal(expected.Length, permissionSet.Requirements.Count);
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(
                expected[index].Permission,
                permissionSet.Requirements[index].Permission.Value);
            Assert.Equal(
                expected[index].ScopeResolver,
                permissionSet.Requirements[index].ScopeResolverName);
            Assert.True(permissionSet.Requirements[index].RequireScope);
        }
    }

    private static void AssertAssurance(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route)
    {
        RouteEndpoint endpoint = FindEndpoint(endpoints, method, route);
        Assert.Contains(
            endpoint.Metadata,
            metadata => string.Equals(
                metadata.GetType().Name,
                "AuthenticationAssuranceMetadata",
                StringComparison.Ordinal));
    }

    private static RouteEndpoint FindEndpoint(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route) =>
        Assert.Single(endpoints, candidate =>
            string.Equals(
                candidate.RoutePattern.RawText?.Trim('/'),
                route.Trim('/'),
                StringComparison.Ordinal) &&
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                method,
                StringComparer.Ordinal) == true);
}
