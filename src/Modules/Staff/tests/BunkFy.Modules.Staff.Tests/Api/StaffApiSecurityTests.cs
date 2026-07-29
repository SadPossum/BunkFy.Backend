namespace BunkFy.Modules.Staff.Tests.Api;

using System.Reflection;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Api;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.AccessControl;
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
public sealed class StaffApiSecurityTests
{
    [Fact]
    public async Task Directory_and_sensitive_profile_routes_have_distinct_permissions()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddOptions<StaffApiSecurityOptions>();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();
        new StaffModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];
        const string member = "/api/staff/members/{staffMemberId:guid}";

        AssertPermissions(endpoints, HttpMethods.Get, member, StaffAdminPermissionCodes.Read);
        AssertPermissions(
            endpoints,
            HttpMethods.Get,
            $"{member}/profile",
            StaffAdminPermissionCodes.SensitiveProfileRead);
        AssertPermissions(
            endpoints,
            HttpMethods.Put,
            member,
            StaffAdminPermissionCodes.Manage,
            StaffAdminPermissionCodes.SensitiveProfileRead);
        AssertPermissions(
            endpoints,
            HttpMethods.Put,
            $"{member}/auth-subject",
            StaffAdminPermissionCodes.Manage,
            StaffAdminPermissionCodes.SensitiveProfileRead);

        AssertResponse<StaffDirectoryListResponse>(endpoints, HttpMethods.Get, "/api/staff/members");
        AssertResponse<StaffDirectoryMemberDto>(endpoints, HttpMethods.Get, member);
        AssertResponse<StaffMemberDto>(endpoints, HttpMethods.Get, $"{member}/profile");
        AssertResponse<StaffMemberDto>(endpoints, HttpMethods.Put, member);
        const string correction = "/api/staff/data-rights-corrections";
        AssertPermissions(
            endpoints,
            HttpMethods.Post,
            correction,
            DataRightsAdminPermissionCodes.Execute);
        AssertResponse<StaffDataRightsCorrectionReceiptDto>(
            endpoints,
            HttpMethods.Post,
            correction);
    }

    [Fact]
    public async Task Governance_and_data_holds_use_dedicated_permissions_and_bounded_assurance()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.Configure<StaffApiSecurityOptions>(options =>
        {
            options.EmploymentGovernanceAssurance =
                new AuthenticationAssuranceRequirement(
                    maxAuthenticationAge: TimeSpan.FromMinutes(10));
            options.DataHoldReleaseAssurance =
                new AuthenticationAssuranceRequirement(
                    maxAuthenticationAge: TimeSpan.FromMinutes(10));
        });
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();
        new StaffModule().MapEndpoints(app);

        RouteEndpoint[] endpoints =
        [
            .. ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(dataSource => dataSource.Endpoints)
                .OfType<RouteEndpoint>()
        ];
        const string member = "/api/staff/{staffMemberId:guid}";
        string governance = $"{member}/employment-governance";
        string holds = $"{member}/data-holds";
        string release = $"{holds}/{{holdId:guid}}/release";

        AssertPermissions(
            endpoints,
            HttpMethods.Get,
            governance,
            StaffAdminPermissionCodes.EmploymentGovernanceManage,
            StaffAdminPermissionCodes.SensitiveProfileRead);
        AssertPermissions(
            endpoints,
            HttpMethods.Put,
            governance,
            StaffAdminPermissionCodes.EmploymentGovernanceManage,
            StaffAdminPermissionCodes.SensitiveProfileRead);
        AssertPermissions(
            endpoints,
            HttpMethods.Get,
            holds,
            StaffAdminPermissionCodes.DataHoldsManage,
            StaffAdminPermissionCodes.SensitiveProfileRead);
        AssertPermissions(
            endpoints,
            HttpMethods.Post,
            holds,
            StaffAdminPermissionCodes.DataHoldsManage,
            StaffAdminPermissionCodes.SensitiveProfileRead);
        AssertPermissions(
            endpoints,
            HttpMethods.Post,
            release,
            StaffAdminPermissionCodes.DataHoldsManage,
            StaffAdminPermissionCodes.SensitiveProfileRead);

        AssertAssurance(endpoints, HttpMethods.Get, governance, false);
        AssertAssurance(endpoints, HttpMethods.Put, governance, true);
        AssertAssurance(endpoints, HttpMethods.Get, holds, false);
        AssertAssurance(endpoints, HttpMethods.Post, holds, false);
        AssertAssurance(endpoints, HttpMethods.Post, release, true);

        AssertResponse<StaffEmploymentGovernanceDto>(
            endpoints,
            HttpMethods.Get,
            governance);
        AssertResponse<StaffEmploymentGovernanceChangeReceiptDto>(
            endpoints,
            HttpMethods.Put,
            governance);
        AssertResponse<StaffDataHoldListResponse>(
            endpoints,
            HttpMethods.Get,
            holds);
        AssertResponse<StaffDataHoldReceiptDto>(
            endpoints,
            HttpMethods.Post,
            holds);
        AssertResponse<StaffDataHoldReceiptDto>(
            endpoints,
            HttpMethods.Post,
            release);
    }

    [Fact]
    public void Sensitive_response_policy_disables_storage()
    {
        Type support = typeof(StaffModule).Assembly.GetType(
            "BunkFy.Modules.Staff.Api.StaffApiEndpointSupport",
            throwOnError: true)!;
        MethodInfo method = support.GetMethod(
            "MarkSensitiveResponse",
            BindingFlags.Public | BindingFlags.Static)!;
        DefaultHttpContext context = new();

        method.Invoke(null, [context]);

        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        Assert.Equal("0", context.Response.Headers.Expires);
    }

    private static void AssertPermissions(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route,
        params string[] expectedPermissions)
    {
        RouteEndpoint endpoint = Assert.Single(endpoints, candidate =>
            string.Equals(
                candidate.RoutePattern.RawText?.Trim('/'),
                route.Trim('/'),
                StringComparison.Ordinal) &&
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                method,
                StringComparer.Ordinal) == true);
        string[] permissions = endpoint.Metadata
            .OfType<AccessPermissionMetadata>()
            .Select(metadata => metadata.Permission.Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedPermissions.Order(StringComparer.Ordinal), permissions);
    }

    private static void AssertAssurance(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route,
        bool expected)
    {
        RouteEndpoint endpoint = Assert.Single(endpoints, candidate =>
            string.Equals(
                candidate.RoutePattern.RawText?.Trim('/'),
                route.Trim('/'),
                StringComparison.Ordinal) &&
            candidate.Metadata
                .GetMetadata<HttpMethodMetadata>()?
                .HttpMethods.Contains(
                    method,
                    StringComparer.Ordinal) == true);
        bool configured = endpoint.Metadata.Any(metadata =>
            string.Equals(
                metadata.GetType().Name,
                "AuthenticationAssuranceMetadata",
                StringComparison.Ordinal));

        Assert.Equal(expected, configured);
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
}
