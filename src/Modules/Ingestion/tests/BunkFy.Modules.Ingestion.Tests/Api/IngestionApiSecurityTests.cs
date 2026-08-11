namespace BunkFy.Modules.Ingestion.Tests.Api;

using System.Reflection;
using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.AdminApi;
using BunkFy.Modules.Ingestion.Api;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.AccessControl.AspNetCore;
using Gma.Framework.Administration.Api;
using Gma.Framework.Cqrs;
using Gma.Framework.Security;
using Gma.Framework.Tenancy;
using Gma.Modules.TaskRuntime.Contracts;
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
    public void Sensitive_response_policies_disable_storage()
    {
        MethodInfo apiPolicy = typeof(IngestionModule).GetMethod(
            "MarkSensitiveResponse",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        MethodInfo adminPolicy = typeof(IngestionAdminApiModule).GetMethod(
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
    public async Task Operational_routes_publish_bounded_response_contracts()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        builder.Services.AddSingleton<AdminApiExecutor>(_ => null!);
        builder.Services.AddSingleton<ITenantContext>(_ => null!);
        builder.Services.AddSingleton<ITaskRunEnqueuer>(_ => null!);
        builder.Services.AddSingleton<ITaskRunReader>(_ => null!);
        builder.Services.AddSingleton<ITaskRunController>(_ => null!);
        await using WebApplication app = builder.Build();

        new IngestionModule().MapEndpoints(app);
        new IngestionAdminApiModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];

        AssertOperationalResponses(endpoints, "/api/ingestion/properties/{propertyId:guid}");
        AssertOperationalResponses(endpoints, "/api/admin/ingestion/properties/{propertyId:guid}");
    }

    [Fact]
    public async Task Configured_assurance_protects_only_sensitive_ingestion_controls()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        AuthenticationAssuranceRequirement assurance = new(
            maxAuthenticationAge: TimeSpan.FromMinutes(10));
        builder.Services.Configure<IngestionApiSecurityOptions>(options =>
        {
            options.CredentialManagementAssurance = assurance;
            options.CheckpointResetAssurance = assurance;
            options.IngressResumeAssurance = assurance;
        });
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

        const string connections =
            "/api/ingestion/properties/{propertyId:guid}/connections/{connectionId:guid}";
        AssertAssurance(
            endpoints,
            HttpMethods.Post,
            $"{connections}/reset-checkpoint",
            expected: true);
        AssertAssurance(endpoints, HttpMethods.Post, $"{connections}/enable", expected: false);
        AssertAssurance(endpoints, HttpMethods.Post, $"{connections}/disable", expected: false);
        AssertAssurance(
            endpoints,
            HttpMethods.Post,
            $"{connections}/polling-schedule/clear",
            expected: false);

        const string ingressControl = "/api/ingestion/adapter-ingress-control";
        AssertAssurance(endpoints, HttpMethods.Get, ingressControl, expected: false);
        AssertAssurance(
            endpoints,
            HttpMethods.Post,
            $"{ingressControl}/suspend",
            expected: false);
        AssertAssurance(
            endpoints,
            HttpMethods.Post,
            $"{ingressControl}/resume",
            expected: true);
    }

    [Fact]
    public void Checkpoint_reset_has_a_dedicated_confirmation_contract()
    {
        IngestionModule.ResetConnectionCheckpointRequest request = new(
            Guid.NewGuid(),
            ExpectedVersion: 7,
            Confirmed: false);

        Assert.False(request.Confirmed);
        Assert.DoesNotContain(
            typeof(IngestionModule.ConnectionControlRequest).GetProperties(),
            property => string.Equals(
                property.Name,
                nameof(IngestionModule.ResetConnectionCheckpointRequest.Confirmed),
                StringComparison.Ordinal));
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
    public async Task Tenant_ingress_control_endpoints_require_the_dedicated_permission()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();

        new IngestionModule().MapEndpoints(app);

        RouteEndpoint[] endpoints = [.. ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()];
        const string control = "/api/ingestion/adapter-ingress-control";
        AssertPermission(
            endpoints,
            HttpMethods.Get,
            control,
            IngestionAdminPermissionCodes.IngressControlManage);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{control}/suspend",
            IngestionAdminPermissionCodes.IngressControlManage);
        AssertPermission(
            endpoints,
            HttpMethods.Post,
            $"{control}/resume",
            IngestionAdminPermissionCodes.IngressControlManage);
    }

    [Fact]
    public async Task Enabled_ingress_endpoints_apply_the_bounded_http_request_limit()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Services.Configure<IngestionAdapterIngressOptions>(options => options.Enabled = true);
        builder.Services.AddSingleton<IRequestDispatcher>(_ => null!);
        builder.Services.AddSingleton<IAccessHttpSubjectResolver>(_ => null!);
        await using WebApplication app = builder.Build();

        new IngestionModule().MapEndpoints(app);

        RouteEndpoint endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>(), candidate =>
                string.Equals(
                    candidate.RoutePattern.RawText?.Trim('/'),
                    "api/ingestion/adapter-ingress/connections/{connectionId:guid}/observations",
                    StringComparison.Ordinal) &&
                candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(
                    HttpMethods.Post,
                    StringComparer.Ordinal) == true);
        IRequestSizeLimitMetadata limit =
            Assert.IsType<IRequestSizeLimitMetadata>(
                endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>(),
                exactMatch: false);

        Assert.Equal(
            AdapterIngressContractLimits.MaximumHttpRequestBodyBytes,
            limit.MaxRequestBodySize);
    }

    [Fact]
    public void Public_submission_bounds_reject_oversized_batches_and_payloads()
    {
        AdapterIngressObservationRequest record = Record([0x7B, 0x7D]);
        AdapterIngressSubmissionRequest oversizedBatch = new(
            Enumerable.Range(0, AdapterProtocolLimits.MaximumRecordsPerSubmission + 1)
                .Select(index => record with { OperationId = GuidFrom(index + 1) })
                .ToArray());
        AdapterIngressSubmissionRequest oversizedPayload = new(
            [
                Record(new byte[AdapterProtocolLimits.MaximumSubmissionPayloadBytes + 1])
            ]);

        Assert.False(IngestionModule.IsValidSubmission(oversizedBatch));
        Assert.False(IngestionModule.IsValidSubmission(oversizedPayload));
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

    private static void AssertOperationalResponses(IEnumerable<RouteEndpoint> endpoints, string routeBase)
    {
        string connections = $"{routeBase}/connections";
        AssertResponse<AdapterConnectionListResponse>(endpoints, HttpMethods.Get, connections);
        AssertResponse<AdapterConnectionDto>(endpoints, HttpMethods.Get, $"{connections}/{{connectionId:guid}}");
        AssertResponse<AdapterConnectionMutationReceiptDto>(endpoints, HttpMethods.Post, connections);
        AssertResponse<AdapterConnectionMutationReceiptDto>(
            endpoints,
            HttpMethods.Put,
            $"{connections}/{{connectionId:guid}}");
        AssertResponse<AdapterConnectionMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{connections}/{{connectionId:guid}}/enable");
        AssertResponse<AdapterIngressCredentialListResponse>(
            endpoints,
            HttpMethods.Get,
            $"{connections}/{{connectionId:guid}}/credentials");
        AssertResponse<CreateAdapterIngressCredentialResponse>(
            endpoints,
            HttpMethods.Post,
            $"{connections}/{{connectionId:guid}}/credentials");
        AssertResponse<AdapterIngressCredentialMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{connections}/{{connectionId:guid}}/credentials/{{credentialId:guid}}/revoke");

        string runs = $"{routeBase}/runs";
        AssertResponse<IngestionRunListResponse>(endpoints, HttpMethods.Get, runs);
        AssertResponse<IngestionRunDto>(endpoints, HttpMethods.Get, $"{runs}/{{runId:guid}}");

        string receipts = $"{routeBase}/receipts";
        AssertResponse<ObservationReceiptListResponse>(endpoints, HttpMethods.Get, receipts);
        AssertResponse<ObservationReceiptDto>(
            endpoints,
            HttpMethods.Get,
            $"{receipts}/{{receiptId:guid}}");

        string attempts = $"{routeBase}/reprocessing-attempts";
        AssertResponse<ObservationReprocessingAttemptListResponse>(endpoints, HttpMethods.Get, attempts);
        AssertResponse<ObservationReprocessingAttemptDetailsDto>(
            endpoints,
            HttpMethods.Get,
            $"{attempts}/{{attemptId:guid}}");

        string proposals = $"{routeBase}/proposals";
        AssertResponse<ChangeProposalListResponse>(endpoints, HttpMethods.Get, proposals);
        AssertResponse<ChangeProposalDto>(endpoints, HttpMethods.Get, $"{proposals}/{{proposalId:guid}}");
        AssertResponse<ChangeProposalMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{proposals}/{{proposalId:guid}}/accept");
        AssertResponse<ChangeProposalMutationReceiptDto>(
            endpoints,
            HttpMethods.Post,
            $"{proposals}/{{proposalId:guid}}/reject");
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

    private static AdapterIngressObservationRequest Record(byte[] payload) => new(
        Guid.NewGuid(),
        "reservation.v1",
        "external-1",
        "revision-1",
        DateTimeOffset.UtcNow,
        DateTimeOffset.UtcNow,
        "application/json",
        payload,
        AdapterPayloadHash.ComputeSha256(payload));

    private static Guid GuidFrom(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }

    private static void AssertNoStore(HttpContext context)
    {
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal("no-cache", context.Response.Headers.Pragma);
        Assert.Equal("0", context.Response.Headers.Expires);
    }
}
