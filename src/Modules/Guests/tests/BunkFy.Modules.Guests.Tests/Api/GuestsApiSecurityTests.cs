namespace BunkFy.Modules.Guests.Tests.Api;

using System.Reflection;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.AdminApi;
using BunkFy.Modules.Guests.Api;
using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Administration.Api;
using Gma.Framework.Cqrs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestsApiSecurityTests
{
    [Fact]
    public void Mutation_receipt_contains_only_bounded_lifecycle_coordinates()
    {
        string[] members = typeof(GuestMutationReceiptDto)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["GuestId", "LastChangedAtUtc", "Status", "Version"],
            members);
    }

    [Fact]
    public void Sensitive_response_policies_disable_storage()
    {
        MethodInfo apiPolicy = typeof(GuestsModule).GetMethod(
            "MarkSensitiveResponse",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo adminPolicy = typeof(GuestsAdminApiModule).GetMethod(
            "MarkSensitiveResponse",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        DefaultHttpContext apiContext = new();
        DefaultHttpContext adminContext = new();

        apiPolicy.Invoke(null, [apiContext]);
        adminPolicy.Invoke(null, [adminContext]);

        AssertNoStore(apiContext);
        AssertNoStore(adminContext);
    }

    [Fact]
    public async Task Operational_routes_publish_explicit_response_contracts()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        builder.Services.AddSingleton<AdminApiExecutor>(_ => null!);
        await using WebApplication app = builder.Build();

        new GuestsModule().MapEndpoints(app);
        new GuestsAdminApiModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];

        AssertOperationalResponses(endpoints, "/api/guests/properties/{propertyId:guid}");
        AssertOperationalResponses(endpoints, "/api/admin/guests/properties/{propertyId:guid}");
    }

    [Fact]
    public async Task Data_rights_correction_requires_execute_at_guest_property_scope()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();

        new GuestsModule().MapEndpoints(app);

        RouteEndpoint endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>(), candidate =>
                string.Equals(
                    candidate.RoutePattern.RawText?.Trim('/'),
                    "api/guests/properties/{propertyId:guid}/data-rights-corrections",
                    StringComparison.Ordinal) &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                    HttpMethods.Post,
                    StringComparer.Ordinal) == true);
        AccessPermissionMetadata permission =
            Assert.Single(endpoint.Metadata.OfType<AccessPermissionMetadata>());
        Assert.Equal(DataRightsAdminPermissionCodes.Execute, permission.Permission.Value);
        Assert.Equal("guests-property", permission.ScopeResolverName);

        IProducesResponseTypeMetadata response = Assert.Single(
            endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == StatusCodes.Status200OK);
        Assert.Equal(typeof(GuestDataRightsCorrectionReceiptDto), response.Type);
    }

    [Theory]
    [InlineData(
        "api/guests/properties/{propertyId:guid}/data-rights-restrictions",
        "POST",
        typeof(GuestProcessingRestrictionReceiptDto))]
    [InlineData(
        "api/guests/properties/{propertyId:guid}/data-rights-restrictions/{restrictionId:guid}/release",
        "POST",
        typeof(GuestProcessingRestrictionReceiptDto))]
    [InlineData(
        "api/guests/properties/{propertyId:guid}/{guestId:guid}/data-rights-restrictions",
        "GET",
        typeof(GuestProcessingRestrictionListResponse))]
    public async Task Data_rights_restrictions_require_restrict_at_guest_property_scope(
        string route,
        string method,
        Type responseType)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();

        new GuestsModule().MapEndpoints(app);

        RouteEndpoint endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>(), candidate =>
                string.Equals(
                    candidate.RoutePattern.RawText?.Trim('/'),
                    route,
                    StringComparison.Ordinal) &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                    method,
                    StringComparer.Ordinal) == true);
        AccessPermissionMetadata permission =
            Assert.Single(endpoint.Metadata.OfType<AccessPermissionMetadata>());
        Assert.Equal(DataRightsAdminPermissionCodes.Restrict, permission.Permission.Value);
        Assert.Equal("guests-property", permission.ScopeResolverName);

        IProducesResponseTypeMetadata response = Assert.Single(
            endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == StatusCodes.Status200OK);
        Assert.Equal(responseType, response.Type);
    }

    [Theory]
    [InlineData(
        "api/guests/properties/{propertyId:guid}/{guestId:guid}/data-holds",
        "POST",
        typeof(GuestDataHoldReceiptDto))]
    [InlineData(
        "api/guests/properties/{propertyId:guid}/{guestId:guid}/data-holds/{holdId:guid}/release",
        "POST",
        typeof(GuestDataHoldReceiptDto))]
    [InlineData(
        "api/guests/properties/{propertyId:guid}/{guestId:guid}/data-holds",
        "GET",
        typeof(GuestDataHoldListResponse))]
    public async Task Data_holds_require_dedicated_permission_at_guest_property_scope(
        string route,
        string method,
        Type responseType)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();

        new GuestsModule().MapEndpoints(app);

        RouteEndpoint endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>(), candidate =>
                string.Equals(
                    candidate.RoutePattern.RawText?.Trim('/'),
                    route,
                    StringComparison.Ordinal) &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                    method,
                    StringComparer.Ordinal) == true);
        AccessPermissionMetadata permission =
            Assert.Single(endpoint.Metadata.OfType<AccessPermissionMetadata>());
        Assert.Equal(GuestsAdminPermissionCodes.DataHoldsManage, permission.Permission.Value);
        Assert.Equal("guests-property", permission.ScopeResolverName);

        IProducesResponseTypeMetadata response = Assert.Single(
            endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == StatusCodes.Status200OK);
        Assert.Equal(responseType, response.Type);
    }

    private static void AssertOperationalResponses(
        IEnumerable<RouteEndpoint> endpoints,
        string routeBase)
    {
        AssertResponse<GuestListResponse>(endpoints, HttpMethods.Get, routeBase);
        AssertResponse<GuestProfileDto>(
            endpoints,
            HttpMethods.Get,
            $"{routeBase}/{{guestId:guid}}");
        AssertResponse<GuestStayHistoryListResponse>(
            endpoints,
            HttpMethods.Get,
            $"{routeBase}/{{guestId:guid}}/stays");
        AssertResponse<GuestMutationReceiptDto>(endpoints, HttpMethods.Post, routeBase);
        AssertResponse<GuestMutationReceiptDto>(
            endpoints,
            HttpMethods.Put,
            $"{routeBase}/{{guestId:guid}}");
        AssertResponse<GuestMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{routeBase}/{{guestId:guid}}/archive");
    }

    private static void AssertResponse<TResponse>(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route)
    {
        RouteEndpoint endpoint = Assert.Single(endpoints, candidate =>
            string.Equals(
                candidate.RoutePattern.RawText?.Trim('/'),
                route.Trim('/'),
                StringComparison.Ordinal) &&
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                method,
                StringComparer.Ordinal) == true);
        IProducesResponseTypeMetadata response = Assert.Single(
            endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == StatusCodes.Status200OK);

        Assert.Equal(typeof(TResponse), response.Type);
    }

    private static void AssertNoStore(HttpContext context)
    {
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        Assert.Equal("0", context.Response.Headers.Expires);
    }
}
