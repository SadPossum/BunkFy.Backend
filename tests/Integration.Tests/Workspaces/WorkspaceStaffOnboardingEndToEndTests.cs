namespace Integration.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using BunkFy.Host.Worker;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Persistence;
using DotNet.Testcontainers.Containers;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Tenancy;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class WorkspaceStaffOnboardingEndToEndTests
{
    private const string GlobalScopeId = "default";
    private const string TenantHeader = "X-Tenant-Id";
    private static readonly TimeSpan AsyncTimeout = TimeSpan.FromSeconds(30);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Invitation_and_approval_enrollment_provision_scoped_staff_and_recover_after_restart()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_workspace_staff_onboarding_tests")
            .Build();
        await postgreSql.StartAsync();

        string connectionString = postgreSql.GetConnectionString();
        string natsConnectionString = AuthTestContainers.GetNatsConnectionString(nats);
        await using AuthTestApplication api = new(
            "PostgreSql",
            connectionString,
            natsConnectionString,
            enableWorkspaceSelfService: true);
        await api.MigrateWorkspaceOnboardingDatabaseAsync().ConfigureAwait(false);
        using HttpClient client = api.CreateClient();
        using IHost initialWorker = CreateWorker(connectionString, natsConnectionString);
        await initialWorker.StartAsync().ConfigureAwait(false);
        bool initialWorkerStopped = false;
        IHost? recoveryWorker = null;

        try
        {
            VerifiedAccount owner = await RegisterVerifiedAsync(
                api, client, "owner@workspace-onboarding.test").ConfigureAwait(false);
            OrganizationMembershipSummaryDto workspace = await CreateWorkspaceAsync(
                client, owner, "Onboarding House", "onboarding-house").ConfigureAwait(false);
            string workspaceId = workspace.Organization.OrganizationId.ToString("D");
            Assert.Equal(OrganizationMembershipRole.Owner, workspace.Membership.Role);
            await WaitForSeedProfilesAsync(client, workspaceId, owner.AccessToken, AsyncTimeout)
                .ConfigureAwait(false);

            PropertyDto propertyA = await CreatePropertyAsync(
                client, workspaceId, owner.AccessToken, "Harbor House", "HBR").ConfigureAwait(false);
            PropertyDto propertyB = await CreatePropertyAsync(
                client, workspaceId, owner.AccessToken, "Garden House", "GRD").ConfigureAwait(false);
            await WaitForWorkspacePropertyProjectionAsync(
                api, workspaceId, [propertyA.PropertyId, propertyB.PropertyId], AsyncTimeout)
                .ConfigureAwait(false);

            VerifiedAccount invited = await RegisterVerifiedAsync(
                api, client, "invited@workspace-onboarding.test").ConfigureAwait(false);
            VerifiedAccount competitor = await RegisterVerifiedAsync(
                api, client, "competitor@workspace-onboarding.test").ConfigureAwait(false);
            Guid invitationId = Guid.NewGuid();
            WorkspaceStaffJoinSourceIssuanceDto invitation = await IssueInvitationAsync(
                client,
                workspaceId,
                owner.AccessToken,
                invitationId,
                "invited@workspace-onboarding.test",
                propertyA.PropertyId).ConfigureAwait(false);
            string invitationToken = Assert.IsType<string>(invitation.Token);

            using (HttpResponseMessage competingAcceptance = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/organization-invitations/accept",
                       GlobalScopeId,
                       competitor.AccessToken,
                       new { token = invitationToken }).ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.Forbidden, competingAcceptance).ConfigureAwait(false);
            }

            WorkspaceStaffOnboardingDto invitationApplication = await SubmitApplicationAsync(
                client,
                invited,
                WorkspaceStaffOnboardingSourceKind.Invitation,
                invitationToken,
                "Ivy Front Desk").ConfigureAwait(false);
            Assert.Equal(WorkspaceStaffOnboardingStatus.Submitted, invitationApplication.Status);

            OrganizationInvitationAcceptanceDto acceptedInvitation;
            using (HttpResponseMessage acceptance = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/organization-invitations/accept",
                       GlobalScopeId,
                       invited.AccessToken,
                       new { token = invitationToken }).ConfigureAwait(false))
            {
                acceptedInvitation = await ReadSuccessAsync<OrganizationInvitationAcceptanceDto>(acceptance)
                    .ConfigureAwait(false);
            }

            Assert.Equal(invited.SubjectId.ToString("D"), acceptedInvitation.Membership.Membership.SubjectId);
            WorkspaceStaffOnboardingDto completedInvitation = await WaitForApplicationStatusAsync(
                client,
                workspace.Organization.OrganizationId,
                invitationId,
                WorkspaceStaffOnboardingSourceKind.Invitation,
                invited.AccessToken,
                WorkspaceStaffOnboardingStatus.Completed,
                AsyncTimeout).ConfigureAwait(false);
            StaffMemberDto invitedStaff = await GetCurrentStaffAsync(
                client, workspaceId, invited.AccessToken).ConfigureAwait(false);
            Assert.Equal(completedInvitation.StaffMemberId, invitedStaff.StaffMemberId);
            Assert.Equal("Ivy Front Desk", invitedStaff.DisplayName);
            await AssertMemberAccessAsync(
                client,
                workspaceId,
                owner.AccessToken,
                invited.SubjectId,
                propertyA.PropertyId).ConfigureAwait(false);
            await AssertPropertyAccessAsync(
                client, workspaceId, invited.AccessToken, propertyA.PropertyId, HttpStatusCode.OK)
                .ConfigureAwait(false);
            await AssertPropertyAccessAsync(
                client, workspaceId, invited.AccessToken, propertyB.PropertyId, HttpStatusCode.Forbidden)
                .ConfigureAwait(false);
            await AssertPropertyCreationDeniedAsync(client, workspaceId, invited.AccessToken)
                .ConfigureAwait(false);

            using (HttpResponseMessage replay = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/organization-invitations/accept",
                       GlobalScopeId,
                       invited.AccessToken,
                       new { token = invitationToken }).ConfigureAwait(false))
            {
                OrganizationInvitationAcceptanceDto replayed =
                    await ReadSuccessAsync<OrganizationInvitationAcceptanceDto>(replay).ConfigureAwait(false);
                Assert.Equal(acceptedInvitation.Membership.Membership.MembershipId,
                    replayed.Membership.Membership.MembershipId);
            }

            StaffMemberDto replayedStaff = await GetCurrentStaffAsync(
                client, workspaceId, invited.AccessToken).ConfigureAwait(false);
            Assert.Equal(invitedStaff.StaffMemberId, replayedStaff.StaffMemberId);

            Guid enrollmentId = Guid.NewGuid();
            WorkspaceStaffJoinSourceIssuanceDto enrollment = await IssueEnrollmentAsync(
                client,
                workspaceId,
                owner.AccessToken,
                enrollmentId,
                propertyB.PropertyId).ConfigureAwait(false);
            string enrollmentToken = Assert.IsType<string>(enrollment.Token);
            VerifiedAccount applicant = await RegisterVerifiedAsync(
                api, client, "qr-applicant@workspace-onboarding.test").ConfigureAwait(false);
            VerifiedAccount capacityApplicant = await RegisterVerifiedAsync(
                api, client, "qr-capacity@workspace-onboarding.test").ConfigureAwait(false);
            await SubmitApplicationAsync(
                client,
                applicant,
                WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
                enrollmentToken,
                "Quinn Reception").ConfigureAwait(false);
            await SubmitApplicationAsync(
                client,
                capacityApplicant,
                WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
                enrollmentToken,
                "Casey Capacity").ConfigureAwait(false);

            OrganizationEnrollmentOutcomeDto pendingClaim;
            using (HttpResponseMessage claim = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/organization-enrollment/claim",
                       GlobalScopeId,
                       applicant.AccessToken,
                       new { token = enrollmentToken }).ConfigureAwait(false))
            {
                pendingClaim = await ReadSuccessAsync<OrganizationEnrollmentOutcomeDto>(claim)
                    .ConfigureAwait(false);
            }

            Assert.Equal(OrganizationEnrollmentClaimStatus.Pending, pendingClaim.Claim.Status);
            Assert.Null(pendingClaim.Membership);
            using (HttpResponseMessage capacityClaim = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/organization-enrollment/claim",
                       GlobalScopeId,
                       capacityApplicant.AccessToken,
                       new { token = enrollmentToken }).ConfigureAwait(false))
            {
                await AssertStatusAsync(HttpStatusCode.Conflict, capacityClaim).ConfigureAwait(false);
            }

            await WaitForApplicationStatusAsync(
                client,
                workspace.Organization.OrganizationId,
                enrollmentId,
                WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
                applicant.AccessToken,
                WorkspaceStaffOnboardingStatus.PendingApproval,
                AsyncTimeout).ConfigureAwait(false);
            await AssertPropertyAccessAsync(
                client, workspaceId, applicant.AccessToken, propertyB.PropertyId, HttpStatusCode.Forbidden)
                .ConfigureAwait(false);

            await initialWorker.StopAsync().ConfigureAwait(false);
            initialWorkerStopped = true;

            OrganizationEnrollmentOutcomeDto approved;
            using (HttpResponseMessage approval = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/organizations/{workspaceId}/join-requests/{pendingClaim.Claim.ClaimId:D}/approve",
                       workspaceId,
                       owner.AccessToken,
                       new { expectedVersion = pendingClaim.Claim.Version }).ConfigureAwait(false))
            {
                approved = await ReadSuccessAsync<OrganizationEnrollmentOutcomeDto>(approval)
                    .ConfigureAwait(false);
            }

            Assert.Equal(OrganizationEnrollmentClaimStatus.Accepted, approved.Claim.Status);
            Assert.NotNull(approved.Membership);
            await AssertPropertyAccessAsync(
                client, workspaceId, applicant.AccessToken, propertyB.PropertyId, HttpStatusCode.Forbidden)
                .ConfigureAwait(false);

            recoveryWorker = CreateWorker(connectionString, natsConnectionString);
            await recoveryWorker.StartAsync().ConfigureAwait(false);
            WorkspaceStaffOnboardingDto completedEnrollment = await WaitForApplicationStatusAsync(
                client,
                workspace.Organization.OrganizationId,
                enrollmentId,
                WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
                applicant.AccessToken,
                WorkspaceStaffOnboardingStatus.Completed,
                AsyncTimeout).ConfigureAwait(false);
            StaffMemberDto enrolledStaff = await GetCurrentStaffAsync(
                client, workspaceId, applicant.AccessToken).ConfigureAwait(false);
            Assert.Equal(completedEnrollment.StaffMemberId, enrolledStaff.StaffMemberId);
            Assert.Equal("Quinn Reception", enrolledStaff.DisplayName);
            await AssertMemberAccessAsync(
                client,
                workspaceId,
                owner.AccessToken,
                applicant.SubjectId,
                propertyB.PropertyId).ConfigureAwait(false);
            await AssertPropertyAccessAsync(
                client, workspaceId, applicant.AccessToken, propertyB.PropertyId, HttpStatusCode.OK)
                .ConfigureAwait(false);
            await AssertPropertyAccessAsync(
                client, workspaceId, applicant.AccessToken, propertyA.PropertyId, HttpStatusCode.Forbidden)
                .ConfigureAwait(false);
        }
        finally
        {
            if (recoveryWorker is not null)
            {
                await recoveryWorker.StopAsync().ConfigureAwait(false);
                recoveryWorker.Dispose();
            }

            if (!initialWorkerStopped)
            {
                await initialWorker.StopAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<VerifiedAccount> RegisterVerifiedAsync(
        AuthTestApplication api,
        HttpClient client,
        string email)
    {
        AuthTokensResponse tokens = await AuthApiClient.RegisterAsync(client, GlobalScopeId, email)
            .ConfigureAwait(false);
        Guid subjectId = GetSubjectId(tokens.AccessToken);
        AuthenticationMethodsResponse methods;
        using (HttpResponseMessage response = await SendAsync(
                   client, HttpMethod.Get, "/api/auth/methods", GlobalScopeId, tokens.AccessToken)
                   .ConfigureAwait(false))
        {
            methods = await ReadSuccessAsync<AuthenticationMethodsResponse>(response).ConfigureAwait(false);
        }

        AuthenticationEmailResponse authenticationEmail = Assert.Single(methods.Emails);
        Assert.False(authenticationEmail.IsVerified);
        using (HttpResponseMessage request = await SendAsync(
                   client,
                   HttpMethod.Post,
                   "/api/auth/email-verification",
                   GlobalScopeId,
                   tokens.AccessToken,
                   new RequestEmailVerificationRequest(authenticationEmail.Id)).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.Accepted, request).ConfigureAwait(false);
        }

        string code = await api.WaitForEmailVerificationCodeAsync(subjectId, AsyncTimeout)
            .ConfigureAwait(false);
        using (HttpResponseMessage confirmation = await SendAsync(
                   client,
                   HttpMethod.Post,
                   "/api/auth/email-verification/confirm",
                   GlobalScopeId,
                   body: new ConfirmEmailVerificationRequest(code)).ConfigureAwait(false))
        {
            await AssertStatusAsync(HttpStatusCode.NoContent, confirmation).ConfigureAwait(false);
        }

        using (HttpResponseMessage response = await SendAsync(
                   client, HttpMethod.Get, "/api/auth/methods", GlobalScopeId, tokens.AccessToken)
                   .ConfigureAwait(false))
        {
            AuthenticationMethodsResponse verified =
                await ReadSuccessAsync<AuthenticationMethodsResponse>(response).ConfigureAwait(false);
            Assert.True(Assert.Single(verified.Emails).IsVerified);
        }

        return new VerifiedAccount(subjectId, tokens.AccessToken);
    }

    private static async Task<OrganizationMembershipSummaryDto> CreateWorkspaceAsync(
        HttpClient client,
        VerifiedAccount owner,
        string name,
        string slug)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/organizations",
            GlobalScopeId,
            owner.AccessToken,
            new { name, slug }).ConfigureAwait(false);
        return await ReadSuccessAsync<OrganizationMembershipSummaryDto>(response).ConfigureAwait(false);
    }

    private static async Task WaitForSeedProfilesAsync(
        HttpClient client,
        string workspaceId,
        string accessToken,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await SendAsync(
                client,
                HttpMethod.Get,
                "/api/workspace-access/profiles?page=1&pageSize=25",
                workspaceId,
                accessToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                WorkspaceAccessProfileListResponse profiles =
                    await ReadSuccessAsync<WorkspaceAccessProfileListResponse>(response).ConfigureAwait(false);
                if (profiles.Items.Any(profile =>
                        profile.Key == WorkspaceAccessProfileSeeds.FrontDeskKey &&
                        profile.Status == WorkspaceAccessProfileStatus.Active))
                {
                    return;
                }
            }
            else if (response.StatusCode != HttpStatusCode.Forbidden)
            {
                await AssertStatusAsync(HttpStatusCode.OK, response).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        throw new TimeoutException("Workspace owner access and seed profiles were not prepared.");
    }

    private static async Task<PropertyDto> CreatePropertyAsync(
        HttpClient client,
        string workspaceId,
        string accessToken,
        string name,
        string code)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/properties",
            workspaceId,
            accessToken,
            new { name, code, timeZoneId = "UTC" }).ConfigureAwait(false);
        return await ReadSuccessAsync<PropertyDto>(response).ConfigureAwait(false);
    }

    private static async Task WaitForWorkspacePropertyProjectionAsync(
        AuthTestApplication api,
        string workspaceId,
        IReadOnlyCollection<Guid> propertyIds,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using IServiceScope scope = api.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(workspaceId);
            Guid[] projected = await scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>()
                .PropertyProjections
                .AsNoTracking()
                .Where(property => propertyIds.Contains(property.Id))
                .Select(property => property.Id)
                .ToArrayAsync()
                .ConfigureAwait(false);
            if (projected.Length == propertyIds.Count)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        throw new TimeoutException("Workspaces did not receive the property projections.");
    }

    private static async Task<WorkspaceStaffJoinSourceIssuanceDto> IssueInvitationAsync(
        HttpClient client,
        string workspaceId,
        string accessToken,
        Guid sourceId,
        string recipientEmail,
        Guid propertyId)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/workspace-staff-enrollment/sources/invitations",
            workspaceId,
            accessToken,
            new
            {
                sourceId,
                recipientEmail,
                lifetimeHours = 24,
                profileKey = WorkspaceAccessProfileSeeds.FrontDeskKey,
                propertyIds = new[] { propertyId }
            }).ConfigureAwait(false);
        return await ReadSuccessAsync<WorkspaceStaffJoinSourceIssuanceDto>(response).ConfigureAwait(false);
    }

    private static async Task<WorkspaceStaffJoinSourceIssuanceDto> IssueEnrollmentAsync(
        HttpClient client,
        string workspaceId,
        string accessToken,
        Guid sourceId,
        Guid propertyId)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/workspace-staff-enrollment/sources/enrollment-links",
            workspaceId,
            accessToken,
            new
            {
                sourceId,
                lifetimeHours = 24,
                maximumClaims = 1,
                approvalMode = OrganizationEnrollmentApprovalMode.RequiresApproval,
                profileKey = WorkspaceAccessProfileSeeds.FrontDeskKey,
                propertyIds = new[] { propertyId }
            }).ConfigureAwait(false);
        return await ReadSuccessAsync<WorkspaceStaffJoinSourceIssuanceDto>(response).ConfigureAwait(false);
    }

    private static async Task<WorkspaceStaffOnboardingDto> SubmitApplicationAsync(
        HttpClient client,
        VerifiedAccount applicant,
        WorkspaceStaffOnboardingSourceKind sourceKind,
        string token,
        string displayName)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/workspace-staff-enrollment/applications",
            GlobalScopeId,
            applicant.AccessToken,
            new
            {
                sourceKind,
                token,
                displayName,
                legalName = $"{displayName} Legal",
                workEmail = (string?)null,
                workPhone = (string?)null,
                employeeNumber = (string?)null,
                jobTitle = "Front desk",
                department = "Operations"
            }).ConfigureAwait(false);
        return await ReadSuccessAsync<WorkspaceStaffOnboardingDto>(response).ConfigureAwait(false);
    }

    private static async Task<WorkspaceStaffOnboardingDto> WaitForApplicationStatusAsync(
        HttpClient client,
        Guid organizationId,
        Guid sourceId,
        WorkspaceStaffOnboardingSourceKind sourceKind,
        string accessToken,
        WorkspaceStaffOnboardingStatus expected,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        string path = $"/api/workspace-staff-enrollment/{organizationId:D}/applications/current" +
            $"?sourceKind={sourceKind}&sourceId={sourceId:D}";
        while (DateTimeOffset.UtcNow < deadline)
        {
            using HttpResponseMessage response = await SendAsync(
                client, HttpMethod.Get, path, GlobalScopeId, accessToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                WorkspaceStaffOnboardingDto application =
                    await ReadSuccessAsync<WorkspaceStaffOnboardingDto>(response).ConfigureAwait(false);
                if (application.Status == expected)
                {
                    return application;
                }

                if (application.Status is WorkspaceStaffOnboardingStatus.Failed or
                    WorkspaceStaffOnboardingStatus.Rejected or WorkspaceStaffOnboardingStatus.Superseded)
                {
                    Assert.Fail($"Onboarding became {application.Status}: {application.FailureCode}");
                }
            }
            else if (response.StatusCode != HttpStatusCode.NotFound)
            {
                await AssertStatusAsync(HttpStatusCode.OK, response).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        throw new TimeoutException($"Onboarding did not reach {expected} for source {sourceId:D}.");
    }

    private static async Task<StaffMemberDto> GetCurrentStaffAsync(
        HttpClient client,
        string workspaceId,
        string accessToken)
    {
        using HttpResponseMessage response = await SendAsync(
            client, HttpMethod.Get, "/api/staff/me", workspaceId, accessToken).ConfigureAwait(false);
        return await ReadSuccessAsync<StaffMemberDto>(response).ConfigureAwait(false);
    }

    private static async Task AssertMemberAccessAsync(
        HttpClient client,
        string workspaceId,
        string ownerToken,
        Guid subjectId,
        Guid propertyId)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/workspace-access/members/{subjectId:D}/access",
            workspaceId,
            ownerToken).ConfigureAwait(false);
        WorkspaceMemberAccessDto access = await ReadSuccessAsync<WorkspaceMemberAccessDto>(response)
            .ConfigureAwait(false);
        WorkspaceMemberAccessAssignmentDto assignment = Assert.Single(access.Assignments);
        Assert.Equal(WorkspaceAccessProfileSeeds.FrontDeskKey, assignment.ProfileKey);
        Assert.Equal(propertyId, assignment.PropertyId);
    }

    private static async Task AssertPropertyAccessAsync(
        HttpClient client,
        string workspaceId,
        string accessToken,
        Guid propertyId,
        HttpStatusCode expected)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/properties/{propertyId:D}",
            workspaceId,
            accessToken).ConfigureAwait(false);
        await AssertStatusAsync(expected, response).ConfigureAwait(false);
    }

    private static async Task AssertPropertyCreationDeniedAsync(
        HttpClient client,
        string workspaceId,
        string accessToken)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            "/api/properties",
            workspaceId,
            accessToken,
            new { name = "Denied House", code = "DEN", timeZoneId = "UTC" }).ConfigureAwait(false);
        await AssertStatusAsync(HttpStatusCode.Forbidden, response).ConfigureAwait(false);
    }

    private static IHost CreateWorker(string connectionString, string natsConnectionString)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder([]);
        builder.Environment.EnvironmentName = "Integration";
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationIdentity:DisplayName"] = "BunkFy Workspace Onboarding Test Worker",
            ["ApplicationIdentity:Namespace"] = "bunkfy",
            ["Persistence:Provider"] = "PostgreSql",
            ["ConnectionStrings:PostgreSql"] = connectionString,
            ["ConnectionStrings:nats"] = natsConnectionString,
            ["Auth:GlobalScopeId"] = GlobalScopeId,
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
            ["Worker:Modules:AccessControl"] = "true",
            ["Worker:Modules:Auth"] = "true",
            ["Worker:Modules:Organizations"] = "true",
            ["Worker:Modules:Staff"] = "true",
            ["Tasks:Worker:Enabled"] = "false"
        });
        AuthTestConfiguration.ConfigureTokenHashing(builder.Configuration);
        builder.AddWorkerHost();
        builder.ValidateModuleComposition();
        return builder.Build();
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        string scopeId,
        string? accessToken = null,
        object? body = null)
    {
        using HttpRequestMessage request = new(method, path);
        request.Headers.Add(TenantHeader, scopeId);
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

    private static Guid GetSubjectId(string accessToken)
    {
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        string? id = token.Claims.FirstOrDefault(claim =>
            claim.Type is ClaimTypes.NameIdentifier or "nameid" or "sub")?.Value;
        Assert.True(Guid.TryParse(id, out Guid parsed));
        return parsed;
    }

    private sealed record VerifiedAccount(Guid SubjectId, string AccessToken);
}
