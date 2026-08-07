namespace BunkFy.Modules.Properties.Tests.Api;

using System.CommandLine;
using System.Reflection;
using BunkFy.Modules.Properties.AdminApi;
using BunkFy.Modules.Properties.AdminCli;
using BunkFy.Modules.Properties.Api;
using BunkFy.Modules.Properties.Contracts;
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
public sealed class PropertiesApiSecurityTests
{
    [Fact]
    public void Topology_write_contracts_require_operation_ids()
    {
        Type[] requestTypes =
        [
            typeof(PropertiesModule.RoomCreateRequest),
            typeof(PropertiesModule.RoomUpdateRequest),
            typeof(PropertiesModule.BedWriteRequest),
            typeof(PropertiesModule.BedBatchWriteRequest),
            typeof(PropertiesAdminApiModule.RoomCreateRequest),
            typeof(PropertiesAdminApiModule.RoomUpdateRequest),
            typeof(PropertiesAdminApiModule.BedWriteRequest),
            typeof(PropertiesAdminApiModule.BedBatchWriteRequest)
        ];

        Assert.All(
            requestTypes,
            requestType => Assert.Equal(
                typeof(Guid),
                requestType.GetProperty("OperationId")?.PropertyType));
    }

    [Fact]
    public void Admin_cli_requires_operation_identity_for_bed_mutations()
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
        new PropertiesAdminCliModule().MapCommands(registry);

        const string propertyId =
            "71000000-0000-0000-0000-000000000001";
        const string roomId =
            "72000000-0000-0000-0000-000000000001";
        const string bedId =
            "73000000-0000-0000-0000-000000000001";
        const string operationId =
            "74000000-0000-0000-0000-000000000001";
        string[][] commands =
        [
            [
                "properties", "beds", "add",
                "--property-id", propertyId,
                "--room-id", roomId,
                "--expected-room-version", "3",
                "--label", "A"
            ],
            [
                "properties", "beds", "add-many",
                "--property-id", propertyId,
                "--room-id", roomId,
                "--expected-room-version", "3",
                "--label", "A", "B"
            ],
            [
                "properties", "beds", "update",
                "--property-id", propertyId,
                "--room-id", roomId,
                "--bed-id", bedId,
                "--expected-room-version", "3",
                "--label", "A"
            ]
        ];

        foreach (string[] command in commands)
        {
            Assert.NotEmpty(root.Parse(command).Errors);
            Assert.Empty(root.Parse([
                .. command,
                "--operation-id",
                operationId
            ]).Errors);
        }
    }

    [Fact]
    public void Sensitive_response_policies_disable_storage()
    {
        MethodInfo apiPolicy = typeof(PropertiesModule).GetMethod(
            "MarkSensitiveResponse",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo adminPolicy = typeof(PropertiesAdminApiModule).GetMethod(
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

        new PropertiesModule().MapEndpoints(app);
        new PropertiesAdminApiModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];

        AssertTopologyResponses(endpoints, "/api/properties");
        AssertTopologyResponses(endpoints, "/api/admin/properties");
        AssertResponse<CountryPolicyListResponse>(
            endpoints,
            HttpMethods.Get,
            "/api/properties/{propertyId:guid}/country-policies");
        AssertResponse<PropertyProcessingStateDto>(
            endpoints,
            HttpMethods.Get,
            "/api/properties/{propertyId:guid}/processing");
        AssertResponse<PropertyMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            "/api/properties/{propertyId:guid}/processing/activate");
        AssertResponse<PropertyMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            "/api/properties/{propertyId:guid}/processing/suspend");
    }

    private static void AssertTopologyResponses(IEnumerable<RouteEndpoint> endpoints, string routeBase)
    {
        AssertResponse<PropertyListResponse>(endpoints, HttpMethods.Get, routeBase);
        AssertResponse<PropertyDto>(endpoints, HttpMethods.Get, $"{routeBase}/{{propertyId:guid}}");
        AssertResponse<PropertyMutationReceiptDto>(endpoints, HttpMethods.Post, routeBase);
        AssertResponse<PropertyMutationReceiptDto>(endpoints, HttpMethods.Put, $"{routeBase}/{{propertyId:guid}}");
        AssertResponse<PropertyMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{routeBase}/{{propertyId:guid}}/retire");

        string rooms = $"{routeBase}/{{propertyId:guid}}/rooms";
        AssertResponse<RoomListResponse>(endpoints, HttpMethods.Get, rooms);
        AssertResponse<RoomMutationReceiptDto>(endpoints, HttpMethods.Post, rooms);
        AssertResponse<RoomDto>(endpoints, HttpMethods.Get, $"{rooms}/{{roomId:guid}}");
        AssertResponse<RoomMutationReceiptDto>(endpoints, HttpMethods.Put, $"{rooms}/{{roomId:guid}}");
        AssertStatus(
            endpoints,
            HttpMethods.Post,
            $"{rooms}/{{roomId:guid}}/retire",
            StatusCodes.Status204NoContent);

        string beds = $"{rooms}/{{roomId:guid}}/beds";
        AssertResponse<BedListResponse>(endpoints, HttpMethods.Get, beds);
        AssertResponse<BedMutationReceiptDto>(endpoints, HttpMethods.Post, beds);
        AssertResponse<BedBatchMutationReceiptDto>(endpoints, HttpMethods.Post, $"{beds}/batch");
        AssertResponse<BedMutationReceiptDto>(endpoints, HttpMethods.Put, $"{beds}/{{bedId:guid}}");
        AssertStatus(
            endpoints,
            HttpMethods.Post,
            $"{beds}/{{bedId:guid}}/retire",
            StatusCodes.Status204NoContent);
    }

    private static void AssertResponse<TResponse>(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route)
    {
        RouteEndpoint endpoint = FindEndpoint(endpoints, method, route);
        IProducesResponseTypeMetadata response = Assert.Single(
            endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == StatusCodes.Status200OK);

        Assert.Equal(typeof(TResponse), response.Type);
    }

    private static void AssertStatus(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route,
        int statusCode)
    {
        RouteEndpoint endpoint = FindEndpoint(endpoints, method, route);
        Assert.Contains(
            endpoint.Metadata.OfType<IProducesResponseTypeMetadata>(),
            metadata => metadata.StatusCode == statusCode);
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

    private static void AssertNoStore(HttpContext context)
    {
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        Assert.Equal("0", context.Response.Headers.Expires);
    }
}
