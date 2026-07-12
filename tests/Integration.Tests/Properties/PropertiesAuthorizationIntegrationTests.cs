namespace Integration.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.ProjectionRebuild;
using Gma.Framework.Runtime;
using Gma.Framework.Tenancy;
using Gma.Modules.Auth.Contracts;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class PropertiesAuthorizationIntegrationTests
{
    private const string TenantA = "tenant-properties-a";
    private const string TenantB = "tenant-properties-b";
    private const string TenantHeader = "X-Tenant-Id";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Real_tokens_and_persisted_grants_enforce_the_properties_scope_matrix()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();

        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_properties_authorization_tests")
            .Build();
        await postgreSql.StartAsync();

        string connectionString = postgreSql.GetConnectionString();
        await using AuthTestApplication api = new(
            "PostgreSql",
            connectionString,
            AuthTestContainers.GetNatsConnectionString(nats));
        await api.MigratePropertiesAuthorizationDatabaseAsync().ConfigureAwait(false);

        await using AdminCliTestApplication admin = new("PostgreSql", connectionString);
        await admin.MigrateAsync().ConfigureAwait(false);

        using HttpClient client = api.CreateClient();

        using (HttpResponseMessage unauthenticated = await SendAsync(
                   client,
                   HttpMethod.Get,
                   TenantA,
                   "/api/properties").ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Unauthorized, unauthenticated).ConfigureAwait(false);
        }

        AuthTokensResponse tenantManagerA = await AuthApiClient.RegisterAsync(
            client,
            TenantA,
            "manager-a@properties.test").ConfigureAwait(false);
        AuthTokensResponse localOperator = await AuthApiClient.RegisterAsync(
            client,
            TenantA,
            "operator@properties.test").ConfigureAwait(false);
        AuthTokensResponse tenantManagerB = await AuthApiClient.RegisterAsync(
            client,
            TenantB,
            "manager-b@properties.test").ConfigureAwait(false);

        Guid tenantManagerAId = GetSubjectId(tenantManagerA.AccessToken);
        Guid localOperatorId = GetSubjectId(localOperator.AccessToken);
        Guid tenantManagerBId = GetSubjectId(tenantManagerB.AccessToken);

        await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "bootstrap", "--actor", "owner", "--yes"));
        await CreatePropertiesRoleAsync(admin, "property-manager").ConfigureAwait(false);
        await CreatePropertiesRoleAsync(admin, "property-operator").ConfigureAwait(false);
        await AssignRoleAsync(admin, tenantManagerAId, "property-manager", $"tenant:{TenantA}").ConfigureAwait(false);
        await AssignRoleAsync(admin, tenantManagerBId, "property-manager", $"tenant:{TenantB}").ConfigureAwait(false);

        PropertyDto propertyA1 = await CreatePropertyAsync(
            client,
            TenantA,
            tenantManagerA.AccessToken,
            "Alpha House",
            "ALPHA").ConfigureAwait(false);
        PropertyDto propertyA2 = await CreatePropertyAsync(
            client,
            TenantA,
            tenantManagerA.AccessToken,
            "Beta House",
            "BETA").ConfigureAwait(false);
        PropertyDto propertyB = await CreatePropertyAsync(
            client,
            TenantB,
            tenantManagerB.AccessToken,
            "Gamma House",
            "GAMMA").ConfigureAwait(false);

        using (HttpResponseMessage tenantList = await SendAsync(
                   client,
                   HttpMethod.Get,
                   TenantA,
                   "/api/properties",
                   tenantManagerA.AccessToken).ConfigureAwait(false))
        {
            PropertyListResponse properties = await ReadSuccessAsync<PropertyListResponse>(tenantList).ConfigureAwait(false);
            Assert.Equal([propertyA1.PropertyId, propertyA2.PropertyId], properties.Properties.Select(property => property.PropertyId));
        }

        using (HttpResponseMessage ungranted = await SendAsync(
                   client,
                   HttpMethod.Get,
                   TenantA,
                   "/api/properties",
                   localOperator.AccessToken).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Forbidden, ungranted).ConfigureAwait(false);
        }

        await AssignRoleAsync(
            admin,
            localOperatorId,
            "property-operator",
            $"tenant:{TenantA}/property:{propertyA1.PropertyId:D}").ConfigureAwait(false);

        using (HttpResponseMessage propertyList = await SendAsync(
                   client,
                   HttpMethod.Get,
                   TenantA,
                   "/api/properties",
                   localOperator.AccessToken).ConfigureAwait(false))
        {
            PropertyListResponse properties = await ReadSuccessAsync<PropertyListResponse>(propertyList).ConfigureAwait(false);
            PropertyDto visible = Assert.Single(properties.Properties);
            Assert.Equal(propertyA1.PropertyId, visible.PropertyId);
        }

        using (HttpResponseMessage exactProperty = await SendAsync(
                   client,
                   HttpMethod.Get,
                   TenantA,
                   $"/api/properties/{propertyA1.PropertyId:D}",
                   localOperator.AccessToken).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.OK, exactProperty).ConfigureAwait(false);
        }

        using (HttpResponseMessage otherProperty = await SendAsync(
                   client,
                   HttpMethod.Get,
                   TenantA,
                   $"/api/properties/{propertyA2.PropertyId:D}",
                   localOperator.AccessToken).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Forbidden, otherProperty).ConfigureAwait(false);
        }

        using (HttpResponseMessage rootCreateDenied = await SendAsync(
                   client,
                   HttpMethod.Post,
                   TenantA,
                   "/api/properties",
                   localOperator.AccessToken,
                   new { name = "Denied House", code = "DENIED", timeZoneId = "UTC" }).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Forbidden, rootCreateDenied).ConfigureAwait(false);
        }

        using (HttpResponseMessage updateProperty = await SendAsync(
                   client,
                   HttpMethod.Put,
                   TenantA,
                   $"/api/properties/{propertyA1.PropertyId:D}",
                   localOperator.AccessToken,
                   new { name = "Alpha House Updated", code = "ALPHA", timeZoneId = "UTC", expectedVersion = 1 }).ConfigureAwait(false))
        {
            PropertyDto updated = await ReadSuccessAsync<PropertyDto>(updateProperty).ConfigureAwait(false);
            Assert.Equal(2, updated.Version);
        }

        RoomDto room;
        using (HttpResponseMessage createRoom = await SendAsync(
                   client,
                   HttpMethod.Post,
                   TenantA,
                   $"/api/properties/{propertyA1.PropertyId:D}/rooms",
                   localOperator.AccessToken,
                   new { name = "Room 101", expectedPropertyVersion = 2, buildingLabel = "Main", floorLabel = "1" }).ConfigureAwait(false))
        {
            room = await ReadSuccessAsync<RoomDto>(createRoom).ConfigureAwait(false);
        }

        BedDto bed;
        using (HttpResponseMessage addBed = await SendAsync(
                   client,
                   HttpMethod.Post,
                   TenantA,
                   $"/api/properties/{propertyA1.PropertyId:D}/rooms/{room.RoomId:D}/beds",
                   localOperator.AccessToken,
                   new { label = "A", expectedRoomVersion = room.Version }).ConfigureAwait(false))
        {
            bed = await ReadSuccessAsync<BedDto>(addBed).ConfigureAwait(false);
            Assert.Equal(room.RoomId, bed.RoomId);
        }

        using (HttpResponseMessage retireBed = await SendAsync(
                   client,
                   HttpMethod.Post,
                   TenantA,
                   $"/api/properties/{propertyA1.PropertyId:D}/rooms/{room.RoomId:D}/beds/{bed.BedId:D}/retire",
                   localOperator.AccessToken,
                   new { confirmed = true, expectedRoomVersion = bed.RoomVersion }).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.NoContent, retireBed).ConfigureAwait(false);
        }

        using (HttpResponseMessage retireRoom = await SendAsync(
                   client,
                   HttpMethod.Post,
                   TenantA,
                   $"/api/properties/{propertyA1.PropertyId:D}/rooms/{room.RoomId:D}/retire",
                   localOperator.AccessToken,
                   new { confirmed = true, expectedVersion = bed.RoomVersion + 1, cascadeBeds = false }).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.NoContent, retireRoom).ConfigureAwait(false);
        }

        using (HttpResponseMessage retireProperty = await SendAsync(
                   client,
                   HttpMethod.Post,
                   TenantA,
                   $"/api/properties/{propertyA1.PropertyId:D}/retire",
                   localOperator.AccessToken,
                   new { confirmed = true, expectedVersion = 3 }).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.NoContent, retireProperty).ConfigureAwait(false);
        }

        using (IServiceScope scope = api.Services.CreateScope())
        {
            ITenantContextAccessor tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>();
            tenantContext.SetTenant(TenantA);

            PropertiesDbContext dbContext = scope.ServiceProvider.GetRequiredService<PropertiesDbContext>();
            var retirementMessage = Assert.Single(await dbContext.OutboxMessages
                .AsNoTracking()
                .Where(message => message.EventType == typeof(PropertyRetiredIntegrationEvent).FullName)
                .ToListAsync()
                .ConfigureAwait(false));

            string subjectPrefix = scope.ServiceProvider
                .GetRequiredService<IOptions<ApplicationIdentityOptions>>()
                .Value.EffectiveNamespace;
            Assert.Equal(PropertiesIntegrationSubjects.CreatePropertyRetired(subjectPrefix), retirementMessage.Subject);
            Assert.Equal(PropertyRetiredIntegrationEvent.EventVersion, retirementMessage.Version);
            Assert.Equal(TenantA, retirementMessage.ScopeId);
            PropertyRetiredIntegrationEvent? retiredEvent = JsonSerializer.Deserialize<PropertyRetiredIntegrationEvent>(
                retirementMessage.Payload,
                JsonOptions);
            Assert.NotNull(retiredEvent);
            Assert.Equal(propertyA1.PropertyId, retiredEvent.PropertyId);
            Assert.Equal(4, retiredEvent.PropertyVersion);

            IPropertiesTopologyProjectionExportSource exportSource =
                scope.ServiceProvider.GetRequiredService<IPropertiesTopologyProjectionExportSource>();
            ProjectionRebuildRequest rebuildRequest = new("inventory-topology", projectionVersion: 1, batchSize: 1);
            ProjectionReadBatch<PropertyTopologyProjectionExport> firstBatch = await exportSource
                .ReadAsync(rebuildRequest, cursor: null, CancellationToken.None)
                .ConfigureAwait(false);
            ProjectionReadBatch<PropertyTopologyProjectionExport> resumedBatch = await exportSource
                .ReadAsync(rebuildRequest, firstBatch.NextCursor, CancellationToken.None)
                .ConfigureAwait(false);

            PropertyTopologyProjectionExport retiredTopology = Assert.Single(firstBatch.Snapshots);
            Assert.Equal(propertyA1.PropertyId, retiredTopology.PropertyId);
            Assert.Equal(PropertyStatus.Retired, retiredTopology.Status);
            Assert.Equal(4, retiredTopology.Version);
            RoomTopologyProjectionExport retiredRoom = Assert.Single(retiredTopology.Rooms);
            Assert.Equal(RoomStatus.Retired, retiredRoom.Status);
            BedTopologyProjectionExport retiredBed = Assert.Single(retiredRoom.Beds);
            Assert.Equal(BedStatus.Retired, retiredBed.Status);
            Assert.Equal(propertyA2.PropertyId, Assert.Single(resumedBatch.Snapshots).PropertyId);
        }

        using (HttpResponseMessage tenantMismatch = await SendAsync(
                   client,
                   HttpMethod.Get,
                   TenantB,
                   "/api/properties",
                   tenantManagerA.AccessToken).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Forbidden, tenantMismatch).ConfigureAwait(false);
        }

        using (HttpResponseMessage crossTenantIdentifier = await SendAsync(
                   client,
                   HttpMethod.Get,
                   TenantA,
                   $"/api/properties/{propertyB.PropertyId:D}",
                   tenantManagerA.AccessToken).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.NotFound, crossTenantIdentifier).ConfigureAwait(false);
        }

        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "unassign",
            "--actor", "owner",
            "--target-kind", "user",
            "--target-id", localOperatorId.ToString("D"),
            "--role", "property-operator",
            "--scope", $"tenant:{TenantA}/property:{propertyA1.PropertyId:D}"));

        using (HttpResponseMessage revoked = await SendAsync(
                   client,
                   HttpMethod.Get,
                   TenantA,
                   "/api/properties",
                   localOperator.AccessToken).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Forbidden, revoked).ConfigureAwait(false);
        }
    }

    private static async Task CreatePropertiesRoleAsync(AdminCliTestApplication admin, string roleName)
    {
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "create",
            "--actor", "owner",
            "--name", roleName));

        foreach (string permission in new[]
                 {
                     PropertiesAdminPermissionCodes.Read,
                     PropertiesAdminPermissionCodes.PropertiesManage,
                     PropertiesAdminPermissionCodes.RoomsManage,
                     PropertiesAdminPermissionCodes.BedsManage
                 })
        {
            await AssertAdminSuccessAsync(admin.ExecuteAsync(
                "admin", "roles", "grant",
                "--actor", "owner",
                "--role", roleName,
                "--permission", permission));
        }
    }

    private static Task<AdminCliResult> AssignRoleAsync(
        AdminCliTestApplication admin,
        Guid subjectId,
        string roleName,
        string scope) =>
        AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin", "roles", "assign",
            "--actor", "owner",
            "--target-kind", "user",
            "--target-id", subjectId.ToString("D"),
            "--role", roleName,
            "--scope", scope));

    private static async Task<PropertyDto> CreatePropertyAsync(
        HttpClient client,
        string tenantId,
        string accessToken,
        string name,
        string code)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            tenantId,
            "/api/properties",
            accessToken,
            new { name, code, timeZoneId = "UTC" }).ConfigureAwait(false);

        return await ReadSuccessAsync<PropertyDto>(response).ConfigureAwait(false);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string tenantId,
        string path,
        string? accessToken = null,
        object? body = null)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Add(TenantHeader, tenantId);

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(
            response.IsSuccessStatusCode,
            $"Expected a successful response but received {(int)response.StatusCode}. Body: {body}");

        T? value = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        Assert.NotNull(value);
        return value;
    }

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(
            response.StatusCode == expected,
            $"Expected {(int)expected} but received {(int)response.StatusCode}. Body: {body}");
    }

    private static async Task<AdminCliResult> AssertAdminSuccessAsync(Task<AdminCliResult> resultTask)
    {
        AdminCliResult result = await resultTask.ConfigureAwait(false);
        Assert.True(
            result.ExitCode == AdminExitCodes.Success,
            $"ExitCode={result.ExitCode}{Environment.NewLine}Output:{Environment.NewLine}{result.Output}{Environment.NewLine}Error:{Environment.NewLine}{result.Error}");
        return result;
    }

    private static Guid GetSubjectId(string accessToken)
    {
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        string? subjectId = token.Claims.FirstOrDefault(claim =>
            string.Equals(claim.Type, ClaimTypes.NameIdentifier, StringComparison.Ordinal) ||
            string.Equals(claim.Type, "nameid", StringComparison.Ordinal) ||
            string.Equals(claim.Type, "sub", StringComparison.Ordinal))?.Value;

        Assert.True(Guid.TryParse(subjectId, out Guid parsedSubjectId));
        return parsedSubjectId;
    }
}
