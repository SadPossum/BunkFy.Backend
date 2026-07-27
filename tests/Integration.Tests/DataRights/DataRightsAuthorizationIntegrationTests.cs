namespace Integration.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using BunkFy.Modules.DataRights.Contracts;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Modules.Auth.Contracts;
using Integration.Tests.Support;
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
        Guid tenantReaderId = GetSubjectId(tenantTokens.AccessToken);
        Guid propertyReaderId = GetSubjectId(propertyTokens.AccessToken);
        await api.SeedOrganizationMembershipAsync(TenantId, tenantReaderId);
        await api.SeedOrganizationMembershipAsync(TenantId, propertyReaderId);
        Guid propertyId = Guid.NewGuid();
        await ConfigureAccessAsync(
            admin,
            tenantReaderId,
            propertyReaderId,
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
    }

    private static async Task ConfigureAccessAsync(
        AdminCliTestApplication admin,
        Guid tenantReaderId,
        Guid propertyReaderId,
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
    {
        using HttpRequestMessage request = new(HttpMethod.Get, path);
        request.Headers.Add(TenantHeader, TenantId);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
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
}
