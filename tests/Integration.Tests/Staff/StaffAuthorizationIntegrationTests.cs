namespace Integration.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using BunkFy.Host.Worker;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Administration;
using Gma.Framework.Administration.Cli;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Gma.Modules.Auth.Contracts;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class StaffAuthorizationIntegrationTests
{
    private const string TenantId = "a4000000-0000-0000-0000-000000000001";
    private const string TenantHeader = "X-Tenant-Id";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Assignment_projects_properties_without_granting_access_and_respects_scope_direction()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_staff_authorization_tests").Build();
        await postgreSql.StartAsync();

        string connectionString = postgreSql.GetConnectionString();
        string natsConnectionString = AuthTestContainers.GetNatsConnectionString(nats);
        await using AuthTestApplication api = new("PostgreSql", connectionString,
            natsConnectionString, disableOutboxPublisher: false);
        await api.MigrateStaffAuthorizationDatabaseAsync().ConfigureAwait(false);
        await using AdminCliTestApplication admin = new("PostgreSql", connectionString);
        await admin.MigrateAsync().ConfigureAwait(false);
        using HttpClient client = api.CreateClient();
        using IHost worker = CreateWorker(connectionString, natsConnectionString);
        await worker.StartAsync().ConfigureAwait(false);

        try
        {
            AuthTokensResponse managerTokens = await AuthApiClient.RegisterAsync(client, TenantId,
                "manager@staff.test").ConfigureAwait(false);
            AuthTokensResponse assignedTokens = await AuthApiClient.RegisterAsync(client, TenantId,
                "assigned@staff.test").ConfigureAwait(false);
            Guid managerId = GetSubjectId(managerTokens.AccessToken);
            Guid assignedUserId = GetSubjectId(assignedTokens.AccessToken);
            await api.SeedOrganizationMembershipAsync(TenantId, managerId).ConfigureAwait(false);
            await api.SeedOrganizationMembershipAsync(TenantId, assignedUserId).ConfigureAwait(false);

            await ConfigureAccessAsync(admin, managerId).ConfigureAwait(false);
            PropertyDto propertyA = await CreatePropertyAsync(client, managerTokens.AccessToken,
                "Staff House A", "STA").ConfigureAwait(false);
            PropertyDto propertyB = await CreatePropertyAsync(client, managerTokens.AccessToken,
                "Staff House B", "STB").ConfigureAwait(false);
            await AssignPropertyRoleAsync(admin, managerId, propertyA.PropertyId).ConfigureAwait(false);
            await WaitForPropertyProjectionAsync(api, propertyA.PropertyId, TimeSpan.FromSeconds(20))
                .ConfigureAwait(false);
            await WaitForPropertyProjectionAsync(api, propertyB.PropertyId, TimeSpan.FromSeconds(20))
                .ConfigureAwait(false);

            StaffMemberDto member;
            using (HttpResponseMessage create = await SendAsync(client, HttpMethod.Post,
                       "/api/staff/members", managerTokens.AccessToken, new
                       {
                           displayName = "Ada Operator",
                           legalName = "Ada Example",
                           workEmail = "ada.operator@example.test",
                           workPhone = "+1 555 0100",
                           employeeNumber = "EMP-100",
                           jobTitle = "Hostel Manager",
                           department = "Operations",
                           authSubjectId = assignedUserId.ToString("D")
                       }).ConfigureAwait(false))
            {
                member = await ReadSuccessAsync<StaffMemberDto>(create).ConfigureAwait(false);
            }

            using (HttpResponseMessage selfRead = await SendAsync(client, HttpMethod.Get,
                       "/api/staff/me", assignedTokens.AccessToken).ConfigureAwait(false))
            {
                StaffMemberDto visible = await ReadSuccessAsync<StaffMemberDto>(selfRead).ConfigureAwait(false);
                Assert.Equal(member.StaffMemberId, visible.StaffMemberId);
            }

            using (HttpResponseMessage selfUpdate = await SendAsync(client, HttpMethod.Put,
                       "/api/staff/me", assignedTokens.AccessToken, new
                       {
                           displayName = "Ada Front Desk",
                           legalName = member.LegalName,
                           workEmail = member.WorkEmail,
                           workPhone = member.WorkPhone,
                           employeeNumber = member.EmployeeNumber,
                           jobTitle = "Front Desk",
                           department = member.Department,
                           expectedVersion = member.Version
                       }).ConfigureAwait(false))
            {
                member = await ReadSuccessAsync<StaffMemberDto>(selfUpdate).ConfigureAwait(false);
                Assert.Equal("Ada Front Desk", member.DisplayName);
                Assert.Equal("Front Desk", member.JobTitle);
            }

            using (HttpResponseMessage unlinkedSelf = await SendAsync(client, HttpMethod.Get,
                       "/api/staff/me", managerTokens.AccessToken).ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.NotFound, unlinkedSelf).ConfigureAwait(false);
            }

            using (HttpResponseMessage assign = await SendAsync(client, HttpMethod.Put,
                       $"/api/staff/properties/{propertyA.PropertyId:D}/members/{member.StaffMemberId:D}/assignment",
                       managerTokens.AccessToken, new
                       {
                           propertyJobTitle = "Duty Manager",
                           isPrimary = true,
                           effectiveFrom = new DateOnly(2026, 7, 12),
                           expectedVersion = member.Version
                       }).ConfigureAwait(false))
            {
                member = await ReadSuccessAsync<StaffMemberDto>(assign).ConfigureAwait(false);
                Assert.True(Assert.Single(member.Assignments).IsCurrent);
            }

            using (HttpResponseMessage assignedUserDenied = await SendAsync(client, HttpMethod.Get,
                       $"/api/staff/properties/{propertyA.PropertyId:D}/members/{member.StaffMemberId:D}",
                       assignedTokens.AccessToken).ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.Forbidden, assignedUserDenied).ConfigureAwait(false);
            }

            using (HttpResponseMessage propertyRead = await SendAsync(client, HttpMethod.Get,
                       $"/api/staff/properties/{propertyA.PropertyId:D}/members/{member.StaffMemberId:D}",
                       managerTokens.AccessToken).ConfigureAwait(false))
            {
                StaffMemberDto visible = await ReadSuccessAsync<StaffMemberDto>(propertyRead).ConfigureAwait(false);
                Assert.Equal(member.StaffMemberId, visible.StaffMemberId);
            }

            using (HttpResponseMessage otherPropertyDenied = await SendAsync(client, HttpMethod.Get,
                       $"/api/staff/properties/{propertyB.PropertyId:D}/members/{member.StaffMemberId:D}",
                       managerTokens.AccessToken).ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.Forbidden, otherPropertyDenied).ConfigureAwait(false);
            }

            using (HttpResponseMessage tenantReadDenied = await SendAsync(client, HttpMethod.Get,
                       $"/api/staff/members/{member.StaffMemberId:D}", managerTokens.AccessToken)
                       .ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.Forbidden, tenantReadDenied).ConfigureAwait(false);
            }

            using (HttpResponseMessage suspend = await SendAsync(client, HttpMethod.Post,
                       $"/api/staff/members/{member.StaffMemberId:D}/suspend", managerTokens.AccessToken,
                       new { reason = "Planned leave", expectedVersion = member.Version }).ConfigureAwait(false))
            {
                member = await ReadSuccessAsync<StaffMemberDto>(suspend).ConfigureAwait(false);
                Assert.Equal(StaffStatus.Suspended, member.Status);
            }

            using (HttpResponseMessage resume = await SendAsync(client, HttpMethod.Post,
                       $"/api/staff/members/{member.StaffMemberId:D}/resume", managerTokens.AccessToken,
                       new { reason = "Returned", expectedVersion = member.Version }).ConfigureAwait(false))
            {
                member = await ReadSuccessAsync<StaffMemberDto>(resume).ConfigureAwait(false);
                Assert.Equal(StaffStatus.Active, member.Status);
            }

            using (HttpResponseMessage unassign = await SendAsync(client, HttpMethod.Post,
                       $"/api/staff/properties/{propertyA.PropertyId:D}/members/{member.StaffMemberId:D}/unassign",
                       managerTokens.AccessToken, new
                       {
                           effectiveTo = new DateOnly(2026, 7, 12),
                           reason = "Transferred",
                           expectedVersion = member.Version
                       }).ConfigureAwait(false))
            {
                member = await ReadSuccessAsync<StaffMemberDto>(unassign).ConfigureAwait(false);
                Assert.False(Assert.Single(member.Assignments).IsCurrent);
            }

            using (HttpResponseMessage noLongerVisible = await SendAsync(client, HttpMethod.Get,
                       $"/api/staff/properties/{propertyA.PropertyId:D}/members/{member.StaffMemberId:D}",
                       managerTokens.AccessToken).ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.NotFound, noLongerVisible).ConfigureAwait(false);
            }

            using IServiceScope verification = api.Services.CreateScope();
            verification.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
            StaffDbContext dbContext = verification.ServiceProvider.GetRequiredService<StaffDbContext>();
            string[] payloads = await dbContext.OutboxMessages.AsNoTracking()
                .Where(message => message.EventType != null && message.EventType.Contains("Staff"))
                .Select(message => message.Payload).ToArrayAsync().ConfigureAwait(false);
            Assert.NotEmpty(payloads);
            Assert.All(payloads, payload =>
            {
                Assert.DoesNotContain("Ada Operator", payload, StringComparison.Ordinal);
                Assert.DoesNotContain("ada.operator@example.test", payload, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Transferred", payload, StringComparison.Ordinal);
            });
            Assert.True(await dbContext.InboxMessages.AsNoTracking().AnyAsync(message =>
                message.Handler == StaffModuleMetadata.PropertyCreatedHandlerName).ConfigureAwait(false));
        }
        finally
        {
            await worker.StopAsync().ConfigureAwait(false);
        }
    }

    private static IHost CreateWorker(string connectionString, string natsConnectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Environment.EnvironmentName = "Integration";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationIdentity:DisplayName"] = "BunkFy Staff Test Worker",
            ["ApplicationIdentity:Namespace"] = "bunkfy",
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] = connectionString,
            ["ConnectionStrings:nats"] = natsConnectionString,
            ["Tenancy:Enabled"] = "true",
            ["Caching:Enabled"] = "false",
            ["NatsJetStream:Enabled"] = "true",
            ["NatsConsumers:Enabled"] = "true",
            ["NatsConsumers:PollInterval"] = "00:00:00.100",
            ["NatsConsumers:AckWait"] = "00:00:05",
            ["NatsConsumers:AckProgressInterval"] = "00:00:01",
            ["NatsConsumers:HandlerTimeout"] = "00:00:10",
            ["NatsConsumers:NakDelay"] = "00:00:00.100",
            ["Worker:Modules:Properties"] = "true",
            ["Worker:Modules:Staff"] = "true",
            ["Tasks:Worker:Enabled"] = "false"
        });
        builder.AddWorkerHost();
        builder.ValidateModuleComposition();
        return builder.Build();
    }

    private static async Task ConfigureAccessAsync(AdminCliTestApplication admin, Guid managerId)
    {
        await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "bootstrap", "--actor", "owner", "--yes"));
        await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "roles", "create", "--actor", "owner",
            "--name", "staff-provisioner"));
        foreach (string permission in new[] { PropertiesAdminPermissionCodes.PropertiesManage,
                     StaffAdminPermissionCodes.Create, StaffAdminPermissionCodes.Manage,
                     StaffAdminPermissionCodes.ManageLifecycle })
        {
            await GrantAsync(admin, "staff-provisioner", permission).ConfigureAwait(false);
        }

        await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "roles", "assign", "--actor", "owner",
            "--target-kind", "user", "--target-id", managerId.ToString("D"),
            "--role", "staff-provisioner", "--scope", $"tenant:{TenantId}"));
        await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "roles", "create", "--actor", "owner",
            "--name", "staff-property-manager"));
        await GrantAsync(admin, "staff-property-manager", StaffAdminPermissionCodes.Read).ConfigureAwait(false);
        await GrantAsync(admin, "staff-property-manager", StaffAdminPermissionCodes.AssignProperties)
            .ConfigureAwait(false);
    }

    private static async Task GrantAsync(AdminCliTestApplication admin, string role, string permission) =>
        await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "roles", "grant", "--actor", "owner",
            "--role", role, "--permission", permission)).ConfigureAwait(false);

    private static async Task AssignPropertyRoleAsync(AdminCliTestApplication admin,
        Guid managerId, Guid propertyId) => await AssertAdminSuccessAsync(admin.ExecuteAsync("admin", "roles",
        "assign", "--actor", "owner", "--target-kind", "user", "--target-id", managerId.ToString("D"),
        "--role", "staff-property-manager", "--scope", $"tenant:{TenantId}/property:{propertyId:D}"))
        .ConfigureAwait(false);

    private static async Task<PropertyDto> CreatePropertyAsync(HttpClient client, string token,
        string name, string code)
    {
        using HttpResponseMessage response = await SendAsync(client, HttpMethod.Post, "/api/properties",
            token, new { name, code, timeZoneId = "UTC" }).ConfigureAwait(false);
        return await ReadSuccessAsync<PropertyDto>(response).ConfigureAwait(false);
    }

    private static async Task WaitForPropertyProjectionAsync(AuthTestApplication api, Guid propertyId,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using IServiceScope scope = api.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
            if (await scope.ServiceProvider.GetRequiredService<StaffDbContext>().PropertyProjections
                    .AsNoTracking().AnyAsync(property => property.Id == propertyId).ConfigureAwait(false))
            {
                return;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException("Staff did not receive the property projection.");
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method,
        string path, string? token = null, object? body = null)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Add(TenantHeader, TenantId);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new("Bearer", token);
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
        Assert.True(response.IsSuccessStatusCode,
            $"Expected success but received {(int)response.StatusCode}. Body: {body}");
        T? value = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        return Assert.IsType<T>(value);
    }

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        Assert.True(response.StatusCode == expected,
            $"Expected {(int)expected} but received {(int)response.StatusCode}. Body: {body}");
    }

    private static async Task<AdminCliResult> AssertAdminSuccessAsync(Task<AdminCliResult> resultTask)
    {
        AdminCliResult result = await resultTask.ConfigureAwait(false);
        Assert.True(result.ExitCode == AdminExitCodes.Success,
            $"ExitCode={result.ExitCode}{Environment.NewLine}Output:{Environment.NewLine}{result.Output}{Environment.NewLine}Error:{Environment.NewLine}{result.Error}");
        return result;
    }

    private static Guid GetSubjectId(string accessToken)
    {
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        string? id = token.Claims.FirstOrDefault(claim => claim.Type is ClaimTypes.NameIdentifier or "nameid" or "sub")?.Value;
        Assert.True(Guid.TryParse(id, out Guid parsed));
        return parsed;
    }
}
