namespace BunkFy.Modules.Ingestion.Tests.Api;

using BunkFy.Modules.Ingestion.Api;
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

    private static void AssertAssurance(
        IEnumerable<RouteEndpoint> endpoints,
        string method,
        string route,
        bool expected)
    {
        RouteEndpoint endpoint = Assert.Single(endpoints, candidate =>
            string.Equals(candidate.RoutePattern.RawText, route, StringComparison.Ordinal) &&
            candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.Ordinal) == true);
        bool configured = endpoint.Metadata.Any(metadata =>
            string.Equals(metadata.GetType().Name, "AuthenticationAssuranceMetadata", StringComparison.Ordinal));
        Assert.Equal(expected, configured);
    }
}
