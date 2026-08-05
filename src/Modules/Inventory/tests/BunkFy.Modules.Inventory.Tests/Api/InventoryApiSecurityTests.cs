namespace BunkFy.Modules.Inventory.Tests.Api;

using System.Reflection;
using BunkFy.Modules.Inventory.AdminApi;
using BunkFy.Modules.Inventory.Api;
using BunkFy.Modules.Inventory.Contracts;
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
public sealed class InventoryApiSecurityTests
{
    [Fact]
    public void Sensitive_response_policies_disable_storage()
    {
        MethodInfo apiPolicy = typeof(InventoryModule).GetMethod(
            "MarkSensitiveResponse",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo adminPolicy = typeof(InventoryAdminApiModule).GetMethod(
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

        new InventoryModule().MapEndpoints(app);
        new InventoryAdminApiModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];

        AssertOperationalResponses(endpoints, "/api/inventory/properties/{propertyId:guid}");
        AssertOperationalResponses(endpoints, "/api/admin/inventory/properties/{propertyId:guid}");
    }

    private static void AssertOperationalResponses(
        IEnumerable<RouteEndpoint> endpoints,
        string routeBase)
    {
        string rooms = $"{routeBase}/rooms";
        AssertResponse<RoomInventoryListResponse>(endpoints, HttpMethods.Get, rooms);
        AssertResponse<RoomInventoryMutationReceiptDto>(
            endpoints,
            HttpMethods.Put,
            $"{rooms}/{{roomId:guid}}/sales-mode");
        AssertResponse<RoomInventoryChangeImpactDto>(
            endpoints,
            HttpMethods.Get,
            $"{rooms}/{{roomId:guid}}/change-impact");
        AssertResponse<InventoryAvailabilityResponse>(
            endpoints,
            HttpMethods.Get,
            $"{routeBase}/availability");

        string blocks = $"{routeBase}/blocks";
        AssertResponse<ManualInventoryBlockListResponse>(endpoints, HttpMethods.Get, blocks);
        AssertResponse<ManualInventoryBlockMutationReceiptDto>(endpoints, HttpMethods.Post, blocks);
        AssertResponse<ManualInventoryBlockMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{blocks}/{{blockId:guid}}/release");
        AssertResponse<ManualInventoryBlockGroupMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{routeBase}/block-groups");
        AssertResponse<ManualInventoryBlockGroupMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{routeBase}/block-groups/{{blockGroupId:guid}}/release");

        AssertResponse<BedRetirementDto>(
            endpoints,
            HttpMethods.Post,
            $"{rooms}/{{roomId:guid}}/beds/{{bedId:guid}}/retirement");
        AssertResponse<BedRetirementDto>(
            endpoints,
            HttpMethods.Get,
            $"{routeBase}/bed-retirements/{{topologyChangeId:guid}}");
        AssertResponse<BedRetirementDto>(
            endpoints,
            HttpMethods.Post,
            $"{routeBase}/bed-retirements/{{topologyChangeId:guid}}/retry");
        AssertResponse<RoomRetirementDto>(
            endpoints,
            HttpMethods.Post,
            $"{rooms}/{{roomId:guid}}/retirement");
        AssertResponse<RoomRetirementDto>(
            endpoints,
            HttpMethods.Get,
            $"{routeBase}/room-retirements/{{topologyChangeId:guid}}");
        AssertResponse<RoomRetirementDto>(
            endpoints,
            HttpMethods.Post,
            $"{routeBase}/room-retirements/{{topologyChangeId:guid}}/retry");
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
