namespace BunkFy.Modules.Reservations.Tests.Api;

using System.Reflection;
using BunkFy.Modules.Reservations.AdminApi;
using BunkFy.Modules.Reservations.Api;
using BunkFy.Modules.Reservations.Contracts;
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
public sealed class ReservationsApiSecurityTests
{
    [Fact]
    public void Personal_data_response_policies_disable_storage()
    {
        MethodInfo apiPolicy = typeof(ReservationsModule).GetMethod(
            "MarkPersonalDataResponse",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo adminPolicy = typeof(ReservationsAdminApiModule).GetMethod(
            "MarkPersonalDataResponse",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        DefaultHttpContext apiContext = new();
        DefaultHttpContext adminContext = new();

        apiPolicy.Invoke(null, [apiContext]);
        adminPolicy.Invoke(null, [adminContext]);

        AssertNoStore(apiContext);
        AssertNoStore(adminContext);
    }

    [Fact]
    public async Task Operational_routes_publish_bounded_response_contracts()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        builder.Services.AddSingleton<AdminApiExecutor>(_ => null!);
        await using WebApplication app = builder.Build();

        new ReservationsModule().MapEndpoints(app);
        new ReservationsAdminApiModule().MapEndpoints(app);

        RouteEndpoint[] endpoints =
        [
            .. ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(dataSource => dataSource.Endpoints)
                .OfType<RouteEndpoint>()
        ];

        const string api = "/api/reservations/properties/{propertyId:guid}";
        AssertResponse<ReservationListResponse>(endpoints, HttpMethods.Get, api);
        AssertResponse<ReservationMutationReceiptDto>(endpoints, HttpMethods.Post, api);
        AssertResponse<ReservationDto>(endpoints, HttpMethods.Get, $"{api}/{{reservationId:guid}}");
        AssertResponse<ReservationDetailsHistoryListResponse>(
            endpoints,
            HttpMethods.Get,
            $"{api}/{{reservationId:guid}}/details-history");
        AssertMutationResponses(endpoints, api, includeGuestDetails: true);

        const string admin = "/api/admin/reservations/properties/{propertyId:guid}";
        AssertResponse<ReservationListResponse>(endpoints, HttpMethods.Get, admin);
        AssertResponse<ReservationMutationReceiptDto>(endpoints, HttpMethods.Post, admin);
        AssertResponse<ReservationDto>(endpoints, HttpMethods.Get, $"{admin}/{{reservationId:guid}}");
        AssertResponse<ReservationDetailsHistoryListResponse>(
            endpoints,
            HttpMethods.Get,
            $"{admin}/{{reservationId:guid}}/details-history");
        AssertMutationResponses(endpoints, admin, includeGuestDetails: false);
    }

    private static void AssertMutationResponses(
        IEnumerable<RouteEndpoint> endpoints,
        string routeBase,
        bool includeGuestDetails)
    {
        string reservation = $"{routeBase}/{{reservationId:guid}}";
        if (includeGuestDetails)
        {
            AssertResponse<ReservationMutationReceiptDto>(
                endpoints,
                HttpMethods.Put,
                $"{reservation}/guest-details");
        }

        AssertResponse<ReservationMutationReceiptDto>(
            endpoints,
            HttpMethods.Put,
            $"{reservation}/inventory");
        AssertResponse<ReservationMutationReceiptDto>(
            endpoints,
            HttpMethods.Put,
            $"{reservation}/guests");
        AssertResponse<ReservationMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{reservation}/cancel");
        AssertResponse<ReservationMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{reservation}/check-in");
        AssertResponse<ReservationMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{reservation}/no-show");
        AssertResponse<ReservationMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{reservation}/check-out");
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
