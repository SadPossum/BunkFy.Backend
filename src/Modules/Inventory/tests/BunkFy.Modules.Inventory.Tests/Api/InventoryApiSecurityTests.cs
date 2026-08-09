namespace BunkFy.Modules.Inventory.Tests.Api;

using System.CommandLine;
using System.Reflection;
using BunkFy.Modules.Inventory.AdminApi;
using BunkFy.Modules.Inventory.AdminCli;
using BunkFy.Modules.Inventory.Api;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Administration.Api;
using Gma.Framework.Administration.Cli;
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
    [Theory]
    [InlineData(typeof(InventoryModule.ConfigureSalesModeRequest))]
    [InlineData(typeof(InventoryAdminApiModule.ConfigureSalesModeRequest))]
    public void Sales_mode_requests_require_caller_owned_operation_identity(
        Type requestType)
    {
        PropertyInfo operationId = requestType.GetProperty("OperationId")!;
        ConstructorInfo constructor = Assert.Single(
            requestType.GetConstructors());
        ParameterInfo parameter = Assert.Single(
            constructor.GetParameters(),
            candidate => string.Equals(
                candidate.Name,
                "operationId",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(typeof(Guid), operationId.PropertyType);
        Assert.Equal(typeof(Guid), parameter.ParameterType);
        Assert.False(parameter.HasDefaultValue);
    }

    [Fact]
    public void Admin_cli_requires_operation_identity_for_room_configuration()
    {
        ServiceCollection services = new();
        services.AddSingleton<AdminCliGlobalOptions>();
        using ServiceProvider provider = services.BuildServiceProvider();
        AdminCliGlobalOptions options = provider
            .GetRequiredService<AdminCliGlobalOptions>();
        RootCommand root = new("admin")
        {
            options.ActorOption,
            options.TenantOption,
            options.OutputOption
        };
        AdminCliCommandRegistry registry = new(root, provider);
        new InventoryAdminCliModule().MapCommands(registry);
        string[] command =
        [
            "inventory", "rooms", "configure",
            "--property-id", "71000000-0000-0000-0000-000000000001",
            "--room-id", "72000000-0000-0000-0000-000000000001",
            "--sales-mode", "room",
            "--expected-version", "1"
        ];

        Assert.NotEmpty(root.Parse(command).Errors);
        Assert.Empty(root.Parse([
            .. command,
            "--operation-id", "73000000-0000-0000-0000-000000000001"
        ]).Errors);
    }

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
