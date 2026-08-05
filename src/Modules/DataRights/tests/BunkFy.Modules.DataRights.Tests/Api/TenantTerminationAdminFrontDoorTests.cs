namespace BunkFy.Modules.DataRights.Tests.Api;

using System.Reflection;
using BunkFy.Modules.DataRights.AdminApi;
using BunkFy.Modules.DataRights.AdminCli;
using Gma.Framework.Administration.Api;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Cqrs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using System.CommandLine.Parsing;
using Xunit;

[Trait("Category", "Unit")]
public sealed class TenantTerminationAdminFrontDoorTests
{
    [Fact]
    public async Task Admin_api_maps_only_the_bounded_operator_routes()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<AdminApiExecutor>(_ => null!);
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        await using WebApplication app = builder.Build();

        new DataRightsAdminApiModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app)
            .DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()];
        string[] routes = endpoints
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
        [
            "/api/admin/data-rights/tenant-termination/processes/{processId:guid}/cancel",
            "/api/admin/data-rights/tenant-termination/processes/{processId:guid}/retry",
            "/api/admin/data-rights/tenant-termination/requests",
            "/api/admin/data-rights/tenant-termination/{caseId:guid}",
            "/api/admin/data-rights/tenant-termination/{caseId:guid}/decision",
            "/api/admin/data-rights/tenant-termination/{caseId:guid}/processes/{processId:guid}/recover",
            "/api/admin/data-rights/tenant-termination/{caseId:guid}/processes/{processId:guid}/start"
        ],
            routes);
        Assert.All(endpoints, endpoint =>
            Assert.NotNull(endpoint.Metadata.GetMetadata<IAuthorizeData>()));
    }

    [Fact]
    public void Admin_api_operator_responses_disable_storage()
    {
        Type endpoints = typeof(DataRightsAdminApiModule).Assembly.GetType(
            "BunkFy.Modules.DataRights.AdminApi.TenantTerminationAdminEndpoints",
            throwOnError: true)!;
        MethodInfo policy = endpoints.GetMethod(
            "ApplySensitiveResponseHeaders",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        DefaultHttpContext context = new();

        policy.Invoke(null, [context.Response]);

        Assert.Equal(
            "no-store, no-cache, max-age=0",
            context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        Assert.Equal("0", context.Response.Headers.Expires);
    }

    [Fact]
    public void Admin_cli_maps_every_operator_control_with_required_evidence()
    {
        ServiceCollection services = new();
        services.AddSingleton<AdminCliGlobalOptions>();
        using ServiceProvider provider = services.BuildServiceProvider();
        AdminCliGlobalOptions options =
            provider.GetRequiredService<AdminCliGlobalOptions>();
        RootCommand root = new("admin")
        {
            options.ActorOption,
            options.TenantOption,
            options.OutputOption
        };
        AdminCliCommandRegistry registry = new(root, provider);

        new DataRightsAdminCliModule().MapCommands(registry);

        string commonId = "72000000-0000-0000-0000-000000000001";
        string processId = "72000000-0000-0000-0000-000000000002";
        string[] evidence =
        [
            "--approval-reference", "approval:change-7201",
            "--owner-catalog-sha256", new string('a', 64),
            "--backup-evidence-reference", "backup:evidence-7201",
            "--restore-drill-evidence-reference", "restore:drill-7201",
            "--operator-assurance-reference", "assurance:operator-7201"
        ];
        string[][] commands =
        [
            ["data-rights", "tenant-termination", "status", "--case-id", commonId],
            ["data-rights", "tenant-termination", "request", "--request-id", commonId, "--relationship", "TenantOwner", "--export", "--yes"],
            ["data-rights", "tenant-termination", "approve", "--case-id", commonId, "--expected-version", "3", "--yes", .. evidence],
            ["data-rights", "tenant-termination", "deny", "--case-id", commonId, "--expected-version", "3", "--reason", "RequestInvalid", "--yes"],
            ["data-rights", "tenant-termination", "start", "--case-id", commonId, "--process-id", processId, "--expected-case-version", "4", "--yes", .. evidence],
            ["data-rights", "tenant-termination", "retry", "--process-id", processId, "--expected-process-version", "5", "--yes"],
            ["data-rights", "tenant-termination", "cancel", "--process-id", processId, "--expected-process-version", "5", "--yes"],
            ["data-rights", "tenant-termination", "recover", "--case-id", commonId, "--process-id", processId, "--expected-case-version", "4", "--expected-process-version", "5", "--yes", .. evidence]
        ];

        Assert.All(commands, command => Assert.Empty(root.Parse(command).Errors));

        ParseResult missingEvidence = root.Parse([
            "data-rights",
            "tenant-termination",
            "approve",
            "--case-id",
            commonId,
            "--expected-version",
            "3",
            "--yes"
        ]);
        Assert.NotEmpty(missingEvidence.Errors);
    }
}
