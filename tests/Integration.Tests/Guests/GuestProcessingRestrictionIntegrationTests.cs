namespace Integration.Tests;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using BunkFy.Modules.DataRights.Persistence;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using BunkFy.Modules.Guests.Persistence;
using BunkFy.Modules.Properties.Contracts;
using DotNet.Testcontainers.Containers;
using Gma.Framework.Administration.Cli;
using Gma.Framework.Tenancy;
using Gma.Modules.Auth.Contracts;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class GuestProcessingRestrictionIntegrationTests
{
    private const string TenantId = "a5000000-0000-0000-0000-000000000001";
    private static readonly Guid PropertyId =
        Guid.Parse("75000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 17, 0, 0, TimeSpan.Zero);

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Approved_apply_and_release_commit_owner_state_and_receipts()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_guest_processing_restriction_tests")
            .Build();
        await postgreSql.StartAsync();

        string connectionString = postgreSql.GetConnectionString();
        await using AuthTestApplication api = new(
            "PostgreSql",
            connectionString,
            AuthTestContainers.GetNatsConnectionString(nats));
        await api.MigrateGuestDataRightsAuthorizationDatabaseAsync().ConfigureAwait(false);
        await using AdminCliTestApplication admin = new("PostgreSql", connectionString);
        await admin.MigrateAsync().ConfigureAwait(false);
        using HttpClient client = api.CreateClient();

        AuthTokensResponse tokens = await AuthApiClient.RegisterAsync(
            client,
            TenantId,
            "privacy-restriction-operator@guests.test").ConfigureAwait(false);
        Guid operatorId = GetSubjectId(tokens.AccessToken);
        await api.SeedOrganizationMembershipAsync(TenantId, operatorId).ConfigureAwait(false);
        await GrantRestrictionAccessAsync(admin, operatorId).ConfigureAwait(false);

        (GuestProfile profile, DataRightsCase applyCase, DataRightsCase releaseCase) =
            await SeedApprovedCasesAsync(api).ConfigureAwait(false);
        GuestProcessingRestrictionReceiptDto applied;
        using (HttpResponseMessage response = await AuthApiClient.PostJsonAsync(
                   client,
                   TenantId,
                   $"/api/guests/properties/{PropertyId:D}/data-rights-restrictions",
                   new
                   {
                       idempotencyKey = Guid.NewGuid(),
                       caseId = applyCase.Id,
                       approvalRevision = applyCase.DecisionRevision!.Value,
                       guestId = profile.Id,
                       expectedGuestVersion = profile.Version,
                       expectedProjectionRevision = 0
                   },
                   tokens.AccessToken).ConfigureAwait(false))
        {
            applied = await ReadSuccessAsync<GuestProcessingRestrictionReceiptDto>(response)
                .ConfigureAwait(false);
        }

        Assert.Equal(GuestProcessingRestrictionActionDto.Apply, applied.Action);
        Assert.True(applied.EffectiveRestricted);
        Assert.Equal(1, applied.RestrictionVersion);
        Assert.Equal(1, applied.ProjectionRevision);

        using (HttpResponseMessage response = await AuthApiClient.GetAsync(
                   client,
                   TenantId,
                   $"/api/guests/properties/{PropertyId:D}/{profile.Id:D}",
                   tokens.AccessToken).ConfigureAwait(false))
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using (HttpResponseMessage response = await AuthApiClient.GetAsync(
                   client,
                   TenantId,
                   $"/api/guests/properties/{PropertyId:D}",
                   tokens.AccessToken).ConfigureAwait(false))
        {
            GuestListResponse visible = await ReadSuccessAsync<GuestListResponse>(response)
                .ConfigureAwait(false);
            Assert.Empty(visible.Guests);
        }

        using (HttpResponseMessage response = await AuthApiClient.GetAsync(
                   client,
                   TenantId,
                   $"/api/guests/properties/{PropertyId:D}/{profile.Id:D}/data-rights-restrictions",
                   tokens.AccessToken).ConfigureAwait(false))
        {
            GuestProcessingRestrictionListResponse active =
                await ReadSuccessAsync<GuestProcessingRestrictionListResponse>(response)
                    .ConfigureAwait(false);
            GuestProcessingRestrictionDto restriction = Assert.Single(active.Restrictions);
            Assert.Equal(applied.RestrictionId, restriction.RestrictionId);
            Assert.Equal(applyCase.Id, restriction.ApplyCaseId);
        }

        GuestProcessingRestrictionReceiptDto released;
        using (HttpResponseMessage response = await AuthApiClient.PostJsonAsync(
                   client,
                   TenantId,
                   $"/api/guests/properties/{PropertyId:D}/data-rights-restrictions/" +
                   $"{applied.RestrictionId:D}/release",
                   new
                   {
                       idempotencyKey = Guid.NewGuid(),
                       caseId = releaseCase.Id,
                       approvalRevision = releaseCase.DecisionRevision!.Value,
                       guestId = profile.Id,
                       expectedGuestVersion = profile.Version,
                       expectedRestrictionVersion = applied.RestrictionVersion,
                       expectedProjectionRevision = applied.ProjectionRevision
                   },
                   tokens.AccessToken).ConfigureAwait(false))
        {
            released = await ReadSuccessAsync<GuestProcessingRestrictionReceiptDto>(response)
                .ConfigureAwait(false);
        }

        Assert.Equal(GuestProcessingRestrictionActionDto.Release, released.Action);
        Assert.False(released.EffectiveRestricted);
        Assert.Equal(2, released.RestrictionVersion);
        Assert.Equal(2, released.ProjectionRevision);

        using (HttpResponseMessage response = await AuthApiClient.GetAsync(
                   client,
                   TenantId,
                   $"/api/guests/properties/{PropertyId:D}/{profile.Id:D}",
                   tokens.AccessToken).ConfigureAwait(false))
        {
            GuestProfileDto visible = await ReadSuccessAsync<GuestProfileDto>(response)
                .ConfigureAwait(false);
            Assert.Equal(profile.Id, visible.GuestId);
        }

        using (HttpResponseMessage response = await AuthApiClient.GetAsync(
                   client,
                   TenantId,
                   $"/api/guests/properties/{PropertyId:D}",
                   tokens.AccessToken).ConfigureAwait(false))
        {
            GuestListResponse visible = await ReadSuccessAsync<GuestListResponse>(response)
                .ConfigureAwait(false);
            Assert.Equal(profile.Id, Assert.Single(visible.Guests).GuestId);
        }

        using IServiceScope verificationScope = api.Services.CreateScope();
        verificationScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(TenantId);
        GuestsDbContext guests =
            verificationScope.ServiceProvider.GetRequiredService<GuestsDbContext>();
        GuestProcessingRestriction restrictionRow = await guests.ProcessingRestrictions
            .AsNoTracking()
            .SingleAsync()
            .ConfigureAwait(false);
        GuestProcessingRestrictionProjection projection =
            await guests.ProcessingRestrictionProjections
                .AsNoTracking()
                .SingleAsync()
                .ConfigureAwait(false);
        GuestProcessingRestrictionReceipt[] receipts =
            await guests.ProcessingRestrictionReceipts
                .AsNoTracking()
                .OrderBy(receipt => receipt.CompletedAtUtc)
                .ThenBy(receipt => receipt.Id)
                .ToArrayAsync()
                .ConfigureAwait(false);

        Assert.Equal(GuestProcessingRestrictionState.Released, restrictionRow.Status);
        Assert.Equal(releaseCase.Id, restrictionRow.ReleaseCaseId);
        Assert.Equal(releaseCase.DecisionRevision, restrictionRow.ReleaseApprovalRevision);
        Assert.False(projection.IsRestricted);
        Assert.Equal(0, projection.ActiveRestrictionCount);
        Assert.Equal(2, projection.Revision);
        Assert.Equal(2, receipts.Length);
        Assert.Contains(receipts, receipt => receipt.Action == GuestProcessingRestrictionAction.Apply);
        Assert.Contains(receipts, receipt => receipt.Action == GuestProcessingRestrictionAction.Release);
    }

    private static async Task<(GuestProfile Profile, DataRightsCase Apply, DataRightsCase Release)>
        SeedApprovedCasesAsync(AuthTestApplication api)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(TenantId);
        GuestsDbContext guests = scope.ServiceProvider.GetRequiredService<GuestsDbContext>();
        guests.PropertyProjections.Add(new GuestPropertyProjection(
            TenantId,
            PropertyId,
            "Restriction House",
            PropertyStatus.Active,
            1));
        await CountryPolicyIntegrationTestData.ApplyActivationAsync(
            scope.ServiceProvider,
            GuestsModuleMetadata.Name,
            TenantId,
            PropertyId,
            2).ConfigureAwait(false);
        GuestProfile profile = GuestProfile.Create(
            Guid.NewGuid(),
            TenantId,
            PropertyId,
            "Restricted Guest",
            null,
            "restricted@example.test",
            null,
            null,
            null,
            null,
            null,
            "user:seed",
            Guid.NewGuid(),
            Now).Value;
        guests.GuestProfiles.Add(profile);
        guests.ProcessingRestrictionProjections.Add(
            GuestProcessingRestrictionProjection.Create(
                TenantId,
                PropertyId,
                profile.Id,
                GuestProcessingRestrictionContract.CurrentVersion,
                Now).Value);
        await guests.SaveChangesAsync().ConfigureAwait(false);

        DataRightsCase apply = CreateApprovedCase(
            profile,
            DataRightsRestrictionAction.Apply,
            Now.AddMinutes(1));
        DataRightsCase release = CreateApprovedCase(
            profile,
            DataRightsRestrictionAction.Release,
            Now.AddMinutes(10));
        DataRightsDbContext dataRights =
            scope.ServiceProvider.GetRequiredService<DataRightsDbContext>();
        dataRights.Cases.AddRange(apply, release);
        await dataRights.SaveChangesAsync().ConfigureAwait(false);
        return (profile, apply, release);
    }

    private static DataRightsCase CreateApprovedCase(
        GuestProfile profile,
        DataRightsRestrictionAction action,
        DateTimeOffset startedAtUtc)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            PropertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Restriction,
            DataRightsRequesterRelation.ControllerInitiated,
            action).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            TenantId,
            request,
            "user:privacy-reviewer",
            startedAtUtc).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            dataRightsCase.Version,
            "user:privacy-reviewer",
            startedAtUtc.AddMinutes(1)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            GuestsDataRightsCoordinates.Owner,
            GuestsDataRightsCoordinates.GuestProfileRecordType,
            profile.Id,
            profile.Version,
            dataRightsCase.Version,
            "user:privacy-reviewer",
            startedAtUtc.AddMinutes(2)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            dataRightsCase.Version,
            "user:privacy-reviewer",
            startedAtUtc.AddMinutes(3)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            dataRightsCase.Version,
            "user:decision-maker",
            startedAtUtc.AddMinutes(4)).IsSuccess);
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            dataRightsCase.Version,
            "user:decision-maker",
            startedAtUtc.AddMinutes(5)).IsSuccess);
        return dataRightsCase;
    }

    private static async Task GrantRestrictionAccessAsync(
        AdminCliTestApplication admin,
        Guid operatorId)
    {
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "bootstrap",
            "--actor",
            "owner",
            "--yes"));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "roles",
            "create",
            "--actor",
            "owner",
            "--name",
            "privacy-restriction-operator"));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "roles",
            "grant",
            "--actor",
            "owner",
            "--role",
            "privacy-restriction-operator",
            "--permission",
            DataRightsAdminPermissionCodes.Restrict));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "roles",
            "grant",
            "--actor",
            "owner",
            "--role",
            "privacy-restriction-operator",
            "--permission",
            GuestsAdminPermissionCodes.Read));
        await AssertAdminSuccessAsync(admin.ExecuteAsync(
            "admin",
            "roles",
            "assign",
            "--actor",
            "owner",
            "--target-kind",
            "user",
            "--target-id",
            operatorId.ToString("D"),
            "--role",
            "privacy-restriction-operator",
            "--scope",
            $"tenant:{TenantId}/property:{PropertyId:D}"));
    }

    private static async Task<T> ReadSuccessAsync<T>(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            Assert.Fail(
                $"Request failed with HTTP {(int)response.StatusCode} ({response.StatusCode}): {body}");
        }

        T? value = await response.Content.ReadFromJsonAsync<T>().ConfigureAwait(false);
        return Assert.IsType<T>(value);
    }

    private static Guid GetSubjectId(string accessToken)
    {
        JwtSecurityToken token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        return Guid.Parse(token.Claims.Single(claim =>
            claim.Type is "sub" or "nameid").Value);
    }

    private static async Task AssertAdminSuccessAsync(Task<AdminCliResult> operation)
    {
        AdminCliResult result = await operation.ConfigureAwait(false);
        Assert.True(
            result.ExitCode == 0,
            $"Admin CLI failed with exit code {result.ExitCode}:{Environment.NewLine}" +
            $"{result.Output}{Environment.NewLine}{result.Error}");
    }
}
