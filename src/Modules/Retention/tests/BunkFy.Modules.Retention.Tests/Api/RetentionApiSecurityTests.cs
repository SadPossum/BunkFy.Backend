namespace BunkFy.Modules.Retention.Tests.Api;

using System.Reflection;
using BunkFy.Modules.Retention.Admin.Contracts;
using BunkFy.Modules.Retention.AdminApi;
using BunkFy.Modules.Retention.Api;
using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Administration.Api;
using Gma.Framework.Cqrs;
using Gma.Framework.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RetentionApiSecurityTests
{
    [Fact]
    public void Sensitive_response_policies_disable_storage()
    {
        MethodInfo apiPolicy = typeof(RetentionModule).GetMethod(
            "MarkSensitiveResponse",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo adminPolicy = typeof(RetentionAdminApiModule).GetMethod(
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
        builder.Services.AddSingleton<AdminApiExecutor>(_ => null!);
        builder.Services.AddSingleton<ITenantContext>(_ => null!);
        await using WebApplication app = builder.Build();

        new RetentionModule().MapEndpoints(app);
        new RetentionAdminApiModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];

        AssertResponse<RetentionScheduleHealthListResponse>(
            endpoints,
            HttpMethods.Get,
            "/api/retention/schedules");
        AssertResponse<RetentionScheduleHealthListResponse>(
            endpoints,
            HttpMethods.Get,
            "/api/admin/retention/schedules");
        AssertResponse<RetentionRunRetryReceiptDto>(
            endpoints,
            HttpMethods.Post,
            "/api/admin/retention/runs/{runId:guid}/retry");
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
