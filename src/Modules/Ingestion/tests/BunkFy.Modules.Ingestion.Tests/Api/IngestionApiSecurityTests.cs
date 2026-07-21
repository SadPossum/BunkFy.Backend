namespace BunkFy.Modules.Ingestion.Tests.Api;

using System.Reflection;
using BunkFy.Modules.Ingestion.Api;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
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
public sealed class IngestionApiSecurityTests
{
    [Fact]
    public async Task Configured_assurance_protects_credential_mutations_only()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.Configure<IngestionApiSecurityOptions>(options =>
            options.CredentialManagementAssurance = new AuthenticationAssuranceRequirement(
                maxAuthenticationAge: TimeSpan.FromMinutes(10)));
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();

        new IngestionModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];
        const string credentials =
            "/api/ingestion/properties/{propertyId:guid}/connections/{connectionId:guid}/credentials";
        AssertAssurance(endpoints, HttpMethods.Post, credentials, expected: true);
        AssertAssurance(endpoints, HttpMethods.Get, credentials, expected: false);
        AssertAssurance(
            endpoints,
            HttpMethods.Post,
            $"{credentials}/{{credentialId:guid}}/revoke",
            expected: true);
    }

    [Fact]
    public async Task Proposal_detail_requires_sensitive_history_permission_while_list_remains_operational()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();

        new IngestionModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];
        const string proposals = "/api/ingestion/properties/{propertyId:guid}/proposals";
        AssertPermission(endpoints, HttpMethods.Get, proposals, IngestionAdminPermissionCodes.Read);
        AssertPermission(
            endpoints,
            HttpMethods.Get,
            $"{proposals}/{{proposalId:guid}}",
            IngestionAdminPermissionCodes.SensitiveHistoryRead);
        AssertPermission(
            endpoints,
            HttpMethods.Get,
            "/api/ingestion/properties/{propertyId:guid}/receipts/{receiptId:guid}/raw-payload",
            IngestionAdminPermissionCodes.RawPayloadsRead);
    }

    [Fact]
    public void Raw_payload_download_is_an_opaque_non_cacheable_sandboxed_attachment()
    {
        MethodInfo method = typeof(IngestionModule).GetMethod(
            "RawPayloadDownload",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        DefaultHttpContext context = new();
        Guid receiptId = Guid.NewGuid();

        object? result = method.Invoke(
            null,
            [context, receiptId, new ObservationRawPayload("text/html", "ignored", "payload"u8.ToArray())]);

        Assert.IsType<IResult>(result, exactMatch: false);
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("nosniff", context.Response.Headers.XContentTypeOptions);
        Assert.Equal("sandbox", context.Response.Headers.ContentSecurityPolicy);
        Assert.Equal("same-origin", context.Response.Headers["Cross-Origin-Resource-Policy"]);
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
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.Ordinal) == true);
        bool configured = endpoint.Metadata.Any(metadata =>
            string.Equals(metadata.GetType().Name, "AuthenticationAssuranceMetadata", StringComparison.Ordinal));
        Assert.Equal(expected, configured);
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
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.Ordinal) == true);
        AccessPermissionMetadata permission = Assert.Single(endpoint.Metadata.OfType<AccessPermissionMetadata>());
        Assert.Equal(expectedPermission, permission.Permission.Value);
    }
}
