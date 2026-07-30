namespace Integration.Tests;

using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using BunkFy.Host.ServiceDefaults.Security;
using BunkFy.Modules.DataRights.Contracts;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Security;
using Gma.Modules.Auth.Contracts;
using Integration.Tests.Support;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class DataRightsAuthorizationIntegrationTests
{
    private const string TenantId = "8d000000-0000-0000-0000-000000000001";
    private const string TenantHeader = "X-Tenant-Id";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Tenant_case_routes_require_a_tenant_grant_and_preserve_property_routes()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_data_rights_authorization_tests")
                .Build();
        await Task.WhenAll(nats.StartAsync(), postgreSql.StartAsync());

        string connectionString = postgreSql.GetConnectionString();
        string natsConnectionString =
            AuthTestContainers.GetNatsConnectionString(nats);
        await using AuthTestApplication api = new(
            "PostgreSql",
            connectionString,
            natsConnectionString);
        await api.MigrateGuestDataRightsAuthorizationDatabaseAsync();
        await using AdminCliTestApplication admin =
            new("PostgreSql", connectionString);
        await admin.MigrateAsync();
        using HttpClient client = api.CreateClient();

        AuthTokensResponse tenantTokens = await AuthApiClient.RegisterAsync(
            client,
            TenantId,
            "tenant-privacy-reader@data-rights.test");
        AuthTokensResponse propertyTokens = await AuthApiClient.RegisterAsync(
            client,
            TenantId,
            "property-privacy-reader@data-rights.test");
        AuthTokensResponse tenantEraserTokens = await AuthApiClient.RegisterAsync(
            client,
            TenantId,
            "tenant-privacy-eraser@data-rights.test");
        Guid tenantReaderId = GetSubjectId(tenantTokens.AccessToken);
        Guid propertyReaderId = GetSubjectId(propertyTokens.AccessToken);
        Guid tenantEraserId = GetSubjectId(tenantEraserTokens.AccessToken);
        await api.SeedOrganizationMembershipAsync(TenantId, tenantReaderId);
        await api.SeedOrganizationMembershipAsync(TenantId, propertyReaderId);
        await api.SeedOrganizationMembershipAsync(TenantId, tenantEraserId);
        Guid propertyId = Guid.NewGuid();
        await ConfigureAccessAsync(
            admin,
            tenantReaderId,
            propertyReaderId,
            tenantEraserId,
            propertyId);

        using HttpResponseMessage tenantAllowed = await SendAsync(
            client,
            "/api/data-rights/tenant/cases",
            tenantTokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, tenantAllowed.StatusCode);

        using HttpResponseMessage propertyGrantDenied = await SendAsync(
            client,
            "/api/data-rights/tenant/cases",
            propertyTokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, propertyGrantDenied.StatusCode);

        using HttpResponseMessage propertyAllowed = await SendAsync(
            client,
            $"/api/data-rights/properties/{propertyId:D}/cases",
            propertyTokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, propertyAllowed.StatusCode);

        Guid missingCaseId = Guid.NewGuid();
        string executionPath =
            $"/api/data-rights/tenant/cases/{missingCaseId:D}/execution";
        using HttpResponseMessage tenantExecutionReadable = await SendAsync(
            client,
            executionPath,
            tenantTokens.AccessToken);
        Assert.Equal(HttpStatusCode.NotFound, tenantExecutionReadable.StatusCode);

        using HttpResponseMessage propertyExecutionDenied = await SendAsync(
            client,
            executionPath,
            propertyTokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, propertyExecutionDenied.StatusCode);

        using HttpResponseMessage tenantReaderCannotErase = await SendAsync(
            client,
            HttpMethod.Post,
            executionPath,
            tenantTokens.AccessToken,
            new
            {
                idempotencyKey = Guid.NewGuid(),
                expectedVersion = 1L,
            });
        Assert.Equal(HttpStatusCode.Forbidden, tenantReaderCannotErase.StatusCode);

        using HttpResponseMessage tenantEraserNeedsAssurance = await SendAsync(
            client,
            HttpMethod.Post,
            executionPath,
            tenantEraserTokens.AccessToken,
            new
            {
                idempotencyKey = Guid.NewGuid(),
                expectedVersion = 1L,
            });
        string insufficientAssuranceBody =
            await tenantEraserNeedsAssurance.Content.ReadAsStringAsync();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            tenantEraserNeedsAssurance.StatusCode);
        Assert.Contains(
            "Security.InsufficientAuthentication",
            insufficientAssuranceBody,
            StringComparison.Ordinal);

        string assuredEraserAccessToken = CreateAssuredAccessToken(
            tenantEraserTokens.AccessToken);
        using HttpResponseMessage tenantEraserCanReachExecution = await SendAsync(
            client,
            HttpMethod.Post,
            executionPath,
            assuredEraserAccessToken,
            new
            {
                idempotencyKey = Guid.NewGuid(),
                expectedVersion = 1L,
            });
        Assert.Equal(
            HttpStatusCode.NotFound,
            tenantEraserCanReachExecution.StatusCode);
    }

    private static async Task ConfigureAccessAsync(
        AdminCliTestApplication admin,
        Guid tenantReaderId,
        Guid propertyReaderId,
        Guid tenantEraserId,
        Guid propertyId)
    {
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "bootstrap",
            "--actor",
            "owner",
            "--yes"));
        await CreateReaderRoleAsync(admin, "tenant-data-rights-reader");
        await CreateReaderRoleAsync(admin, "property-data-rights-reader");
        await CreateEraserRoleAsync(admin, "tenant-data-rights-eraser");
        await AssignRoleAsync(
            admin,
            tenantReaderId,
            "tenant-data-rights-reader",
            $"tenant:{TenantId}");
        await AssignRoleAsync(
            admin,
            propertyReaderId,
            "property-data-rights-reader",
            $"tenant:{TenantId}/property:{propertyId:D}");
        await AssignRoleAsync(
            admin,
            tenantEraserId,
            "tenant-data-rights-eraser",
            $"tenant:{TenantId}");
    }

    private static async Task CreateReaderRoleAsync(
        AdminCliTestApplication admin,
        string role)
    {
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "roles",
            "create",
            "--actor",
            "owner",
            "--name",
            role));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "roles",
            "grant",
            "--actor",
            "owner",
            "--role",
            role,
            "--permission",
            DataRightsAdminPermissionCodes.Read));
    }

    private static async Task CreateEraserRoleAsync(
        AdminCliTestApplication admin,
        string role)
    {
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "roles",
            "create",
            "--actor",
            "owner",
            "--name",
            role));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "roles",
            "grant",
            "--actor",
            "owner",
            "--role",
            role,
            "--permission",
            DataRightsAdminPermissionCodes.Erase));
    }

    private static async Task AssignRoleAsync(
        AdminCliTestApplication admin,
        Guid subjectId,
        string role,
        string scope) =>
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "roles",
            "assign",
            "--actor",
            "owner",
            "--target-kind",
            "user",
            "--target-id",
            subjectId.ToString("D"),
            "--role",
            role,
            "--scope",
            scope));

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string path,
        string accessToken)
        => await SendAsync(
            client,
            HttpMethod.Get,
            path,
            accessToken,
            body: null);

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string accessToken,
        object? body)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Add(TenantHeader, TenantId);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private static async Task<AdminCliResult> AssertAdminSuccessAsync(
        Task<AdminCliResult> resultTask)
    {
        AdminCliResult result = await resultTask;
        Assert.True(
            result.ExitCode == AdminExitCodes.Success,
            $"ExitCode={result.ExitCode}{Environment.NewLine}" +
            $"Output:{Environment.NewLine}{result.Output}{Environment.NewLine}" +
            $"Error:{Environment.NewLine}{result.Error}");
        return result;
    }

    private static Guid GetSubjectId(string accessToken)
    {
        JwtSecurityToken token =
            new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        string? id = token.Claims.FirstOrDefault(claim =>
            claim.Type is ClaimTypes.NameIdentifier or "nameid" or "sub")?.Value;
        Assert.True(Guid.TryParse(id, out Guid parsed));
        return parsed;
    }

    private static string CreateAssuredAccessToken(string accessToken)
    {
        JwtSecurityToken source =
            new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        string[] replacedClaims =
        [
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Nbf,
            JwtRegisteredClaimNames.Exp,
            JwtRegisteredClaimNames.Iss,
            JwtRegisteredClaimNames.Aud,
            ApplicationClaimNames.AuthenticationContextReference,
            ApplicationClaimNames.AuthenticationTime,
        ];
        List<Claim> claims = source.Claims
            .Where(claim =>
                !replacedClaims.Contains(claim.Type, StringComparer.Ordinal))
            .ToList();
        claims.Add(new Claim(
            ApplicationClaimNames.AuthenticationContextReference,
            BunkFyAuthenticationAssurance.TwoStepContextReference));
        claims.Add(new Claim(
            ApplicationClaimNames.AuthenticationTime,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                .ToString(CultureInfo.InvariantCulture),
            ClaimValueTypes.Integer64));

        SigningCredentials credentials = new(
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(AuthTestApplication.JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);
        JwtSecurityToken assuredToken = new(
            issuer: "BunkFy",
            audience: "BunkFy",
            claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: source.ValidTo,
            credentials);
        return new JwtSecurityTokenHandler().WriteToken(assuredToken);
    }
}
