namespace Integration.Tests;

using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using BunkFy.Host.Worker;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Workspaces.Application;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain;
using BunkFy.Modules.Workspaces.Persistence;
using DotNet.Testcontainers.Containers;
using Gma.Framework.AccessControl;
using Gma.Framework.Cqrs;
using Gma.Framework.ModuleComposition;
using Gma.Framework.Results;
using Gma.Framework.Tenancy;
using Gma.Modules.AccessControl.Application;
using Gma.Modules.AccessControl.Application.Commands;
using Gma.Modules.AccessControl.Contracts;
using Gma.Modules.Auth.Contracts;
using Gma.Modules.Organizations.Contracts;
using Gma.Modules.Organizations.Domain.Aggregates;
using Gma.Modules.Organizations.Persistence;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    public async Task Manager_two_property_onboarding_completes_with_exact_idempotent_access()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("bunkfy_workspace_manager_onboarding_tests")
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
        AssertRecoveryServicesComposed(api.Services);
        using HttpClient client = api.CreateClient();
        using IHost worker = CreateWorker(connectionString, natsConnectionString);
        await worker.StartAsync().ConfigureAwait(false);

        try
        {
            VerifiedAccount owner = await RegisterVerifiedAsync(
                api, client, "owner-manager-path@workspace-onboarding.test").ConfigureAwait(false);
            OrganizationMembershipSummaryDto workspace = await CreateWorkspaceAsync(
                client, owner, "Manager Path House", "manager-path-house").ConfigureAwait(false);
            string workspaceId = workspace.Organization.OrganizationId.ToString("D");
            await WaitForSeedProfilesAsync(client, workspaceId, owner.AccessToken, AsyncTimeout)
                .ConfigureAwait(false);

            PropertyMutationReceiptDto propertyA = await CreatePropertyAsync(
                client, workspaceId, owner.AccessToken, "Manager Harbor", "MHA").ConfigureAwait(false);
            PropertyMutationReceiptDto propertyB = await CreatePropertyAsync(
                client, workspaceId, owner.AccessToken, "Manager Garden", "MGA").ConfigureAwait(false);
            PropertyMutationReceiptDto propertyC = await CreatePropertyAsync(
                client, workspaceId, owner.AccessToken, "Unassigned Summit", "USM").ConfigureAwait(false);
            Guid[] propertyIds = [propertyA.PropertyId, propertyB.PropertyId];
            await WaitForWorkspacePropertyProjectionAsync(
                api,
                workspaceId,
                [propertyA.PropertyId, propertyB.PropertyId, propertyC.PropertyId],
                AsyncTimeout).ConfigureAwait(false);

            VerifiedAccount manager = await RegisterVerifiedAsync(
                api, client, "manager-two-property@workspace-onboarding.test").ConfigureAwait(false);
            Guid invitationId = Guid.NewGuid();
            WorkspaceStaffJoinSourceIssuanceDto invitation = await IssueInvitationAsync(
                client,
                workspaceId,
                owner.AccessToken,
                invitationId,
                "manager-two-property@workspace-onboarding.test",
                WorkspaceAccessProfileSeeds.ManagerKey,
                propertyIds).ConfigureAwait(false);
            string invitationToken = Assert.IsType<string>(invitation.Token);
            Assert.Equal(WorkspaceAccessProfileSeeds.ManagerKey, invitation.Plan.ProfileKey);
            Assert.Equal(propertyIds.Order(), invitation.Plan.PropertyIds.Order());

            await SubmitApplicationAsync(
                client,
                manager,
                WorkspaceStaffOnboardingSourceKind.Invitation,
                invitationToken,
                "Morgan Manager").ConfigureAwait(false);
            using (HttpResponseMessage acceptance = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/organization-invitations/accept",
                       GlobalScopeId,
                       manager.AccessToken,
                       new { token = invitationToken }).ConfigureAwait(false))
            {
                await ReadSuccessAsync<OrganizationInvitationAcceptanceDto>(acceptance)
                    .ConfigureAwait(false);
            }

            WorkspaceStaffOnboardingDto terminal = await WaitForApplicationTerminalStatusAsync(
                api,
                client,
                workspace.Organization.OrganizationId,
                invitationId,
                WorkspaceStaffOnboardingSourceKind.Invitation,
                manager.AccessToken,
                AsyncTimeout).ConfigureAwait(false);
            if (terminal.Status == WorkspaceStaffOnboardingStatus.Failed)
            {
                Result<ScopedAccessProfileAssignmentReconciliationDetails> underlying =
                    await ProbeManagerReconciliationAsync(
                        api,
                        workspaceId,
                        manager.SubjectId,
                        invitation.Plan.ProfileId,
                        propertyIds).ConfigureAwait(false);
                Assert.False(underlying.IsSuccess);
                Assert.Equal(
                    AccessControlApplicationErrors.ProfilePermissionEscalation.Code,
                    underlying.Error.Code);
                Assert.Fail(
                    "Manager onboarding discarded the underlying reconciliation Result " +
                    $"'{underlying.Error.Code}' and exposed only '{terminal.FailureCode}'.");
            }

            Assert.Equal(WorkspaceStaffOnboardingStatus.Completed, terminal.Status);
            StaffMemberDto managerStaff = await GetCurrentStaffAsync(
                client, workspaceId, manager.AccessToken).ConfigureAwait(false);
            Assert.Equal(terminal.StaffMemberId, managerStaff.StaffMemberId);
            Assert.Equal("Morgan Manager", managerStaff.DisplayName);
            await AssertMemberAccessAsync(
                client,
                workspaceId,
                owner.AccessToken,
                manager.SubjectId,
                WorkspaceAccessProfileSeeds.ManagerKey,
                propertyIds).ConfigureAwait(false);

            await AssertPropertyAccessAsync(
                client, workspaceId, manager.AccessToken, propertyA.PropertyId, HttpStatusCode.OK)
                .ConfigureAwait(false);
            await AssertPropertyAccessAsync(
                client, workspaceId, manager.AccessToken, propertyB.PropertyId, HttpStatusCode.OK)
                .ConfigureAwait(false);
            await AssertPropertyAccessAsync(
                client, workspaceId, manager.AccessToken, propertyC.PropertyId, HttpStatusCode.Forbidden)
                .ConfigureAwait(false);

            using (HttpResponseMessage replay = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/organization-invitations/accept",
                       GlobalScopeId,
                       manager.AccessToken,
                       new { token = invitationToken }).ConfigureAwait(false))
            {
                await ReadSuccessAsync<OrganizationInvitationAcceptanceDto>(replay)
                    .ConfigureAwait(false);
            }

            await AssertMemberAccessAsync(
                client,
                workspaceId,
                owner.AccessToken,
                manager.SubjectId,
                WorkspaceAccessProfileSeeds.ManagerKey,
                propertyIds).ConfigureAwait(false);
            StaffMemberDto replayedManagerStaff = await GetCurrentStaffAsync(
                client, workspaceId, manager.AccessToken).ConfigureAwait(false);
            Assert.Equal(terminal.StaffMemberId, replayedManagerStaff.StaffMemberId);
            Assert.Equal("Morgan Manager", replayedManagerStaff.DisplayName);
            await AssertPropertyAccessAsync(
                client, workspaceId, manager.AccessToken, propertyA.PropertyId, HttpStatusCode.OK)
                .ConfigureAwait(false);
            await AssertPropertyAccessAsync(
                client, workspaceId, manager.AccessToken, propertyB.PropertyId, HttpStatusCode.OK)
                .ConfigureAwait(false);
            await AssertPropertyAccessAsync(
                client, workspaceId, manager.AccessToken, propertyC.PropertyId, HttpStatusCode.Forbidden)
                .ConfigureAwait(false);
        }
        finally
        {
            await worker.StopAsync().ConfigureAwait(false);
        }
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Manager_two_property_exact_partial_recovery_is_receipted_and_replay_is_read_free()
    {
        await using IContainer nats = AuthTestContainers.CreateNatsContainer();
        await nats.StartAsync();
        await using PostgreSqlContainer postgreSql = new PostgreSqlBuilder(
            "postgres:16-alpine")
            .WithDatabase("bunkfy_workspace_manager_recovery_tests")
            .Build();
        await postgreSql.StartAsync();

        string connectionString = postgreSql.GetConnectionString();
        string natsConnectionString = AuthTestContainers.GetNatsConnectionString(nats);
        StaffProofRevisionBarrierInterceptor staffProofBarrier = new();
        await using AuthTestApplication api = new(
            "PostgreSql",
            connectionString,
            natsConnectionString,
            enableWorkspaceSelfService: true,
            staffCommandInterceptor: staffProofBarrier,
            enableWorkspaceStaffOnboardingRecovery: true);
        await api.MigrateWorkspaceOnboardingDatabaseAsync().ConfigureAwait(false);
        using HttpClient client = api.CreateClient();
        using IHost worker = CreateWorker(connectionString, natsConnectionString);
        await worker.StartAsync().ConfigureAwait(false);

        try
        {
            VerifiedAccount owner = await RegisterVerifiedAsync(
                api,
                client,
                "owner-manager-recovery@workspace-onboarding.test")
                .ConfigureAwait(false);
            OrganizationMembershipSummaryDto workspace = await CreateWorkspaceAsync(
                client,
                owner,
                "Manager Recovery House",
                "manager-recovery-house").ConfigureAwait(false);
            string workspaceId = workspace.Organization.OrganizationId.ToString("D");
            await WaitForSeedProfilesAsync(
                client,
                workspaceId,
                owner.AccessToken,
                AsyncTimeout).ConfigureAwait(false);
            PropertyMutationReceiptDto propertyA = await CreatePropertyAsync(
                client,
                workspaceId,
                owner.AccessToken,
                "Recovery Harbor",
                "RHA").ConfigureAwait(false);
            PropertyMutationReceiptDto propertyB = await CreatePropertyAsync(
                client,
                workspaceId,
                owner.AccessToken,
                "Recovery Garden",
                "RGA").ConfigureAwait(false);
            Guid[] propertyIds = [propertyA.PropertyId, propertyB.PropertyId];
            await WaitForWorkspacePropertyProjectionAsync(
                api,
                workspaceId,
                propertyIds,
                AsyncTimeout).ConfigureAwait(false);

            VerifiedAccount manager = await RegisterVerifiedAsync(
                api,
                client,
                "manager-recovery@workspace-onboarding.test")
                .ConfigureAwait(false);
            Guid invitationId = Guid.NewGuid();
            WorkspaceStaffJoinSourceIssuanceDto invitation = await IssueInvitationAsync(
                client,
                workspaceId,
                owner.AccessToken,
                invitationId,
                "manager-recovery@workspace-onboarding.test",
                WorkspaceAccessProfileSeeds.ManagerKey,
                propertyIds).ConfigureAwait(false);
            string invitationToken = Assert.IsType<string>(invitation.Token);
            await SubmitApplicationAsync(
                client,
                manager,
                WorkspaceStaffOnboardingSourceKind.Invitation,
                invitationToken,
                "Riley Manager").ConfigureAwait(false);
            using (HttpResponseMessage acceptance = await SendAsync(
                       client,
                       HttpMethod.Post,
                       "/api/organization-invitations/accept",
                       GlobalScopeId,
                       manager.AccessToken,
                       new { token = invitationToken }).ConfigureAwait(false))
            {
                await ReadSuccessAsync<OrganizationInvitationAcceptanceDto>(acceptance)
                    .ConfigureAwait(false);
            }

            WorkspaceStaffOnboardingDto completed =
                await WaitForApplicationStatusAsync(
                    api,
                    client,
                    workspace.Organization.OrganizationId,
                    invitationId,
                    WorkspaceStaffOnboardingSourceKind.Invitation,
                    manager.AccessToken,
                    WorkspaceStaffOnboardingStatus.Completed,
                    AsyncTimeout).ConfigureAwait(false);
            Guid staffMemberId = Assert.IsType<Guid>(completed.StaffMemberId);
            WorkspaceStaffOnboardingDto failed = await InduceFailedPartialAsync(
                api,
                workspaceId,
                completed.ApplicationId).ConfigureAwait(false);
            Assert.Equal(WorkspaceStaffOnboardingStatus.Failed, failed.Status);
            Assert.Equal(staffMemberId, failed.StaffMemberId);

            ExternalProofCoordinates before = await ObserveExternalProofAsync(
                api,
                workspaceId,
                failed.ApplicationId,
                staffMemberId,
                manager.SubjectId,
                propertyIds).ConfigureAwait(false);
            Guid recoveryOperationId = Guid.NewGuid();
            using HttpResponseMessage recovery = await SendAsync(
                client,
                HttpMethod.Post,
                $"/api/workspace-staff-enrollment/applications/{failed.ApplicationId:D}/recovery-attempts",
                workspaceId,
                owner.AccessToken,
                new
                {
                    operationId = recoveryOperationId,
                    expectedApplicationVersion = failed.Version
                }).ConfigureAwait(false);
            WorkspaceStaffOnboardingRecoveryAttemptDto recovered =
                await ReadSuccessAsync<WorkspaceStaffOnboardingRecoveryAttemptDto>(
                    recovery).ConfigureAwait(false);
            Assert.Equal(
                WorkspaceStaffOnboardingRecoveryAttemptOutcome.Recovered,
                recovered.Outcome);
            Assert.Equal(
                WorkspaceStaffOnboardingRecoveryAttemptReason.ExactStateRecovered,
                recovered.Reason);
            Assert.Equal(WorkspaceStaffOnboardingStatus.Completed, recovered.ApplicationStatus);
            Assert.Equal(failed.Version + 1, recovered.ApplicationVersion);

            ExternalProofCoordinates after = await ObserveExternalProofAsync(
                api,
                workspaceId,
                failed.ApplicationId,
                staffMemberId,
                manager.SubjectId,
                propertyIds).ConfigureAwait(false);
            Assert.Equal(before, after);
            WorkspaceStaffOnboardingRecoveryReceipt receipt = await ReadRecoveryReceiptAsync(
                api,
                workspaceId,
                recoveryOperationId).ConfigureAwait(false);
            Assert.Equal(WorkspaceStaffOnboardingRecoveryOutcome.Recovered, receipt.Outcome);
            Assert.Equal(failed.Version, receipt.SelectedApplicationVersion);
            Assert.Equal(failed.Version + 1, receipt.ResultingApplicationVersion);
            Assert.Equal(propertyIds.Length, receipt.PropertyCount);
            Assert.False(receipt.AccessMutationPerformed);
            Assert.Equal(before.OrganizationRevision, receipt.OrganizationRevision);
            Assert.Equal(before.StaffTenantRevision, receipt.StaffTenantRevision);
            Assert.Equal(before.AccessManagementRevision, receipt.AccessManagementRevision);

            using HttpResponseMessage replayResponse = await SendAsync(
                client,
                HttpMethod.Post,
                $"/api/workspace-staff-enrollment/applications/{failed.ApplicationId:D}/recovery-attempts",
                workspaceId,
                owner.AccessToken,
                new
                {
                    operationId = recoveryOperationId,
                    expectedApplicationVersion = failed.Version
                }).ConfigureAwait(false);
            WorkspaceStaffOnboardingRecoveryAttemptDto replay =
                await ReadSuccessAsync<WorkspaceStaffOnboardingRecoveryAttemptDto>(
                    replayResponse).ConfigureAwait(false);
            Assert.Equal(recovered, replay);
            ExternalProofCoordinates afterReplay = await ObserveExternalProofAsync(
                api,
                workspaceId,
                failed.ApplicationId,
                staffMemberId,
                manager.SubjectId,
                propertyIds).ConfigureAwait(false);
            Assert.Equal(after, afterReplay);
            StaffOnboardingStateVerificationResult revisionChanged =
                await ObserveStaffRevisionInterleavingAsync(
                    api,
                    staffProofBarrier,
                    workspaceId,
                    failed.ApplicationId,
                    staffMemberId,
                    manager.SubjectId,
                    propertyIds).ConfigureAwait(false);
            Assert.Equal(
                StaffOnboardingStateVerificationStatus.RetryLater,
                revisionChanged.Status);
            Assert.Equal(
                StaffOnboardingStateVerificationReason.RevisionChanged,
                revisionChanged.Reason);
            Assert.Equal(
                1,
                await CountRecoveryReceiptsAsync(
                    api,
                    workspaceId,
                    failed.ApplicationId).ConfigureAwait(false));

            using (HttpResponseMessage latest = await SendAsync(
                       client,
                       HttpMethod.Get,
                       $"/api/workspace-staff-enrollment/applications/{failed.ApplicationId:D}/recovery-attempts/latest",
                       workspaceId,
                       owner.AccessToken).ConfigureAwait(false))
            {
                Assert.Equal(recovered,
                    await ReadSuccessAsync<WorkspaceStaffOnboardingRecoveryAttemptDto>(
                        latest).ConfigureAwait(false));
            }

            using (HttpResponseMessage propertyScopedManager = await SendAsync(
                       client,
                       HttpMethod.Post,
                       $"/api/workspace-staff-enrollment/applications/{failed.ApplicationId:D}/recovery-attempts",
                       workspaceId,
                       manager.AccessToken,
                       new
                       {
                           operationId = Guid.NewGuid(),
                           expectedApplicationVersion = recovered.ApplicationVersion
                       }).ConfigureAwait(false))
            {
                await AssertStatusAsync(
                    HttpStatusCode.Forbidden,
                    propertyScopedManager).ConfigureAwait(false);
            }

            WorkspaceStaffOnboardingDto driftCandidate =
                await InduceFailedPartialAsync(
                    api,
                    workspaceId,
                    failed.ApplicationId).ConfigureAwait(false);
            await SetStaffAuthSubjectAsync(
                api,
                workspaceId,
                staffMemberId,
                "auth:test-drift").ConfigureAwait(false);
            WorkspaceStaffOnboardingRecoveryAttemptDto staffConflict =
                await AttemptRecoveryReviewAsync(
                    client,
                    workspaceId,
                    owner.AccessToken,
                    driftCandidate,
                    WorkspaceStaffOnboardingRecoveryAttemptReason.StaffConflict)
                    .ConfigureAwait(false);
            Assert.Equal(driftCandidate.Version, staffConflict.ApplicationVersion);
            await SetStaffAuthSubjectAsync(
                api,
                workspaceId,
                staffMemberId,
                manager.SubjectId.ToString("D")).ConfigureAwait(false);

            await SetLegacyAccessAsync(
                api,
                workspaceId,
                manager.SubjectId,
                present: true).ConfigureAwait(false);
            WorkspaceStaffOnboardingRecoveryAttemptDto accessConflict =
                await AttemptRecoveryReviewAsync(
                    client,
                    workspaceId,
                    owner.AccessToken,
                    driftCandidate,
                    WorkspaceStaffOnboardingRecoveryAttemptReason.AccessConflict)
                    .ConfigureAwait(false);
            Assert.Equal(driftCandidate.Version, accessConflict.ApplicationVersion);
            await SetLegacyAccessAsync(
                api,
                workspaceId,
                manager.SubjectId,
                present: false).ConfigureAwait(false);

            await SuspendMembershipAsync(
                api,
                workspace.Organization.OrganizationId,
                manager.SubjectId).ConfigureAwait(false);
            WorkspaceStaffOnboardingRecoveryAttemptDto membershipConflict =
                await AttemptRecoveryReviewAsync(
                    client,
                    workspaceId,
                    owner.AccessToken,
                    driftCandidate,
                    WorkspaceStaffOnboardingRecoveryAttemptReason.MembershipConflict)
                    .ConfigureAwait(false);
            Assert.Equal(driftCandidate.Version, membershipConflict.ApplicationVersion);
            Assert.Equal(
                4,
                await CountRecoveryReceiptsAsync(
                    api,
                    workspaceId,
                    failed.ApplicationId).ConfigureAwait(false));
        }
        finally
        {
            await worker.StopAsync().ConfigureAwait(false);
        }
    }

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

            PropertyMutationReceiptDto propertyA = await CreatePropertyAsync(
                client, workspaceId, owner.AccessToken, "Harbor House", "HBR").ConfigureAwait(false);
            PropertyMutationReceiptDto propertyB = await CreatePropertyAsync(
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
            AssertActiveAccessPlan(
                invitation,
                invitationId,
                WorkspaceStaffOnboardingSourceKind.Invitation,
                propertyA.PropertyId);
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
                api,
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
            AssertActiveAccessPlan(
                enrollment,
                enrollmentId,
                WorkspaceStaffOnboardingSourceKind.EnrollmentLink,
                propertyB.PropertyId);
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
                api,
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
                api,
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
            new { operationId = Guid.NewGuid(), name, slug }).ConfigureAwait(false);
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

    private static async Task<PropertyMutationReceiptDto> CreatePropertyAsync(
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
            new
            {
                operationId = Guid.NewGuid(),
                name,
                code,
                timeZoneId = "UTC"
            }).ConfigureAwait(false);
        return await ReadSuccessAsync<PropertyMutationReceiptDto>(response).ConfigureAwait(false);
    }

    private static async Task WaitForWorkspacePropertyProjectionAsync(
        AuthTestApplication api,
        string workspaceId,
        Guid[] propertyIds,
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
            if (projected.Length == propertyIds.Length)
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
        => await IssueInvitationAsync(
            client,
            workspaceId,
            accessToken,
            sourceId,
            recipientEmail,
            WorkspaceAccessProfileSeeds.FrontDeskKey,
            [propertyId]).ConfigureAwait(false);

    private static async Task<WorkspaceStaffJoinSourceIssuanceDto> IssueInvitationAsync(
        HttpClient client,
        string workspaceId,
        string accessToken,
        Guid sourceId,
        string recipientEmail,
        string profileKey,
        IReadOnlyCollection<Guid> propertyIds)
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
                profileKey,
                propertyIds
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

    private static void AssertActiveAccessPlan(
        WorkspaceStaffJoinSourceIssuanceDto issuance,
        Guid sourceId,
        WorkspaceStaffOnboardingSourceKind sourceKind,
        Guid propertyId)
    {
        Assert.Equal(sourceId, issuance.Plan.SourceId);
        Assert.Equal(sourceKind, issuance.Plan.SourceKind);
        Assert.Equal(WorkspaceAccessProfileSeeds.FrontDeskKey, issuance.Plan.ProfileKey);
        Assert.Equal(WorkspaceStaffAccessPlanStatus.Active, issuance.Plan.Status);
        Assert.Equal([propertyId], issuance.Plan.PropertyIds);
    }

    private static async Task<WorkspaceStaffOnboardingDto> WaitForApplicationStatusAsync(
        AuthTestApplication api,
        HttpClient client,
        Guid organizationId,
        Guid sourceId,
        WorkspaceStaffOnboardingSourceKind sourceKind,
        string accessToken,
        WorkspaceStaffOnboardingStatus expected,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        WorkspaceStaffOnboardingDto? lastObserved = null;
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
                lastObserved = application;
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

        string observed = lastObserved is null
            ? "no application was visible"
            : $"last state was {lastObserved.Status} at version {lastObserved.Version}" +
              (string.IsNullOrWhiteSpace(lastObserved.FailureCode)
                  ? string.Empty
                  : $" with failure '{lastObserved.FailureCode}'");
        string messaging = await DescribeMessagingStateAsync(api, organizationId)
            .ConfigureAwait(false);
        throw new TimeoutException(
            $"Onboarding did not reach {expected} for source {sourceId:D}; " +
            $"{observed}. {messaging}");
    }

    private static async Task<WorkspaceStaffOnboardingDto> WaitForApplicationTerminalStatusAsync(
        AuthTestApplication api,
        HttpClient client,
        Guid organizationId,
        Guid sourceId,
        WorkspaceStaffOnboardingSourceKind sourceKind,
        string accessToken,
        TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeout);
        WorkspaceStaffOnboardingDto? lastObserved = null;
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
                lastObserved = application;
                if (application.Status is WorkspaceStaffOnboardingStatus.Completed or
                    WorkspaceStaffOnboardingStatus.Failed)
                {
                    return application;
                }
            }
            else if (response.StatusCode != HttpStatusCode.NotFound)
            {
                await AssertStatusAsync(HttpStatusCode.OK, response).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100)).ConfigureAwait(false);
        }

        string observed = lastObserved is null
            ? "no application was visible"
            : $"last state was {lastObserved.Status} at version {lastObserved.Version}";
        string messaging = await DescribeMessagingStateAsync(api, organizationId)
            .ConfigureAwait(false);
        throw new TimeoutException(
            $"Onboarding did not reach a terminal state for source {sourceId:D}; " +
            $"{observed}. {messaging}");
    }

    private static async Task<WorkspaceStaffOnboardingDto> InduceFailedPartialAsync(
        AuthTestApplication api,
        string workspaceId,
        Guid applicationId)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(workspaceId);
        WorkspacesDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<WorkspacesDbContext>();
        DateTimeOffset changedAtUtc = DateTimeOffset.UtcNow;
        int affected = await dbContext.StaffOnboardingApplications
            .Where(application => application.Id == applicationId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    application => application.Status,
                    WorkspaceStaffOnboardingState.Failed)
                .SetProperty(
                    application => application.FailureCode,
                    "Test.InducedAfterExactExternalState")
                .SetProperty(
                    application => application.VerifiedAccountEmail,
                    "manager-recovery@workspace-onboarding.test")
                .SetProperty(
                    application => application.DisplayName,
                    "Riley Manager")
                .SetProperty(
                    application => application.Version,
                    application => application.Version + 1)
                .SetProperty(
                    application => application.LastChangedAtUtc,
                    changedAtUtc))
            .ConfigureAwait(false);
        Assert.Equal(1, affected);
        WorkspaceStaffOnboarding application = await dbContext
            .StaffOnboardingApplications
            .AsNoTracking()
            .SingleAsync(item => item.Id == applicationId)
            .ConfigureAwait(false);
        Assert.NotNull(application.AcceptedMembershipId);
        Assert.NotNull(application.AcceptedMembershipVersion);
        return application.ToDto();
    }

    private static async Task<ExternalProofCoordinates> ObserveExternalProofAsync(
        AuthTestApplication api,
        string workspaceId,
        Guid applicationId,
        Guid staffMemberId,
        Guid subjectId,
        Guid[] propertyIds)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(workspaceId);
        OrganizationScopeSnapshot organization = await scope.ServiceProvider
            .GetRequiredService<IOrganizationScopeLifecycle>()
            .GetSnapshotAsync(Guid.Parse(workspaceId), CancellationToken.None)
            .ConfigureAwait(false);
        StaffOnboardingStateVerificationResult staff = await scope
            .ServiceProvider
            .GetServices<IStaffOnboardingStateVerifier>()
            .Single()
            .VerifyAsync(
                new(
                    workspaceId,
                    applicationId,
                    staffMemberId,
                    subjectId.ToString("D"),
                    propertyIds),
                CancellationToken.None).ConfigureAwait(false);
        AccessControlSubjectAuthoritySnapshot access = await scope
            .ServiceProvider
            .GetRequiredService<IAccessControlSubjectAuthoritySnapshotReader>()
            .ReadAsync(
                new(
                    AccessSubjectKind.User,
                    subjectId.ToString("D"),
                    WorkspaceAccessScopes.Create(workspaceId),
                    propertyIds.Length == 0
                        ? [WorkspaceAccessScopes.Create(workspaceId)]
                        : propertyIds.Select(propertyId =>
                            WorkspaceAccessScopes.CreateProperty(
                                workspaceId,
                                propertyId)).ToArray()),
                CancellationToken.None).ConfigureAwait(false);

        Assert.Equal(OrganizationScopeStatus.Open, organization.Status);
        Assert.Equal(
            StaffOnboardingStateVerificationStatus.Equivalent,
            staff.Status);
        Assert.Equal(AccessControlAuthoritySnapshotStatus.Stable, access.Status);
        Assert.NotNull(staff.StateProofSha256);
        Assert.NotNull(access.FactsSha256);
        return new(
            organization.Revision,
            Assert.IsType<long>(staff.TenantRevision),
            Assert.IsType<long>(access.ManagementRevision),
            Assert.IsType<string>(staff.StateProofSha256),
            Assert.IsType<string>(access.FactsSha256));
    }

    private static async Task<StaffOnboardingStateVerificationResult>
        ObserveStaffRevisionInterleavingAsync(
            AuthTestApplication api,
            StaffProofRevisionBarrierInterceptor barrier,
            string workspaceId,
            Guid applicationId,
            Guid staffMemberId,
            Guid subjectId,
            Guid[] propertyIds)
    {
        using IServiceScope proofScope = api.Services.CreateScope();
        proofScope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(workspaceId);
        IStaffOnboardingStateVerifier verifier = proofScope.ServiceProvider
            .GetServices<IStaffOnboardingStateVerifier>()
            .Single();
        barrier.Arm();
        Task<StaffOnboardingStateVerificationResult> verification =
            verifier.VerifyAsync(
                new(
                    workspaceId,
                    applicationId,
                    staffMemberId,
                    subjectId.ToString("D"),
                    propertyIds),
                CancellationToken.None);
        try
        {
            await barrier.Reached.WaitAsync(AsyncTimeout).ConfigureAwait(false);
            await AdvanceStaffProfileVersionAsync(
                api,
                workspaceId,
                staffMemberId).ConfigureAwait(false);
        }
        finally
        {
            barrier.Release();
        }

        return await verification.ConfigureAwait(false);
    }

    private static async Task AdvanceStaffProfileVersionAsync(
        AuthTestApplication api,
        string workspaceId,
        Guid staffMemberId)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(workspaceId);
        StaffDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        BunkFy.Modules.Staff.Domain.Aggregates.StaffMember member =
            await dbContext.StaffMembers
                .SingleAsync(candidate => candidate.Id == staffMemberId)
                .ConfigureAwait(false);
        Result updated = member.UpdateProfile(
            member.DisplayName,
            member.LegalName,
            member.WorkEmail,
            member.WorkPhone,
            member.EmployeeNumber,
            "Revision fence probe",
            member.Department,
            member.Version,
            "system:integration-test",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        Assert.True(updated.IsSuccess, updated.Error.Code);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task<WorkspaceStaffOnboardingRecoveryReceipt>
        ReadRecoveryReceiptAsync(
            AuthTestApplication api,
            string workspaceId,
            Guid operationId)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(workspaceId);
        return await scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>()
            .StaffOnboardingRecoveryReceipts
            .AsNoTracking()
            .SingleAsync(receipt => receipt.OperationId == operationId)
            .ConfigureAwait(false);
    }

    private static void AssertRecoveryServicesComposed(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<IStaffOnboardingStateVerifier>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<IAccessControlSubjectAuthoritySnapshotReader>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<IScopedAccessProfileManager>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<IWorkspaceStaffOnboardingRecoveryOperationLock>());
        Assert.NotNull(scope.ServiceProvider
            .GetRequiredService<IWorkspaceStaffOnboardingRecoveryReceiptRepository>());
    }

    private static async Task<int> CountRecoveryReceiptsAsync(
        AuthTestApplication api,
        string workspaceId,
        Guid applicationId)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(workspaceId);
        return await scope.ServiceProvider.GetRequiredService<WorkspacesDbContext>()
            .StaffOnboardingRecoveryReceipts
            .AsNoTracking()
            .CountAsync(receipt => receipt.ApplicationId == applicationId)
            .ConfigureAwait(false);
    }

    private static async Task<WorkspaceStaffOnboardingRecoveryAttemptDto>
        AttemptRecoveryReviewAsync(
            HttpClient client,
            string workspaceId,
            string ownerAccessToken,
            WorkspaceStaffOnboardingDto application,
            WorkspaceStaffOnboardingRecoveryAttemptReason expectedReason)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Post,
            $"/api/workspace-staff-enrollment/applications/{application.ApplicationId:D}/recovery-attempts",
            workspaceId,
            ownerAccessToken,
            new
            {
                operationId = Guid.NewGuid(),
                expectedApplicationVersion = application.Version
            }).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync()
            .ConfigureAwait(false);
        Assert.True(
            response.StatusCode == HttpStatusCode.Conflict,
            $"Expected 409 but received {(int)response.StatusCode}. Body: {body}");
        WorkspaceStaffOnboardingRecoveryAttemptDto? attempt =
            JsonSerializer.Deserialize<WorkspaceStaffOnboardingRecoveryAttemptDto>(
                body,
                JsonSerializerOptions.Web);
        WorkspaceStaffOnboardingRecoveryAttemptDto result =
            Assert.IsType<WorkspaceStaffOnboardingRecoveryAttemptDto>(attempt);
        Assert.Equal(
            WorkspaceStaffOnboardingRecoveryAttemptOutcome.ReviewRequired,
            result.Outcome);
        Assert.Equal(expectedReason, result.Reason);
        Assert.Equal(WorkspaceStaffOnboardingStatus.Failed, result.ApplicationStatus);
        return result;
    }

    private static async Task SetStaffAuthSubjectAsync(
        AuthTestApplication api,
        string workspaceId,
        Guid staffMemberId,
        string authSubjectId)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>()
            .SetTenant(workspaceId);
        int affected = await scope.ServiceProvider
            .GetRequiredService<StaffDbContext>()
            .StaffMembers
            .Where(member => member.Id == staffMemberId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                member => member.AuthSubjectId,
                authSubjectId))
            .ConfigureAwait(false);
        Assert.Equal(1, affected);
    }

    private static async Task SetLegacyAccessAsync(
        AuthTestApplication api,
        string workspaceId,
        Guid subjectId,
        bool present)
    {
        using IServiceScope scope = api.Services.CreateScope();
        IAccessControlRoleProvisioner roles = scope.ServiceProvider
            .GetRequiredService<IAccessControlRoleProvisioner>();
        AccessSubject subject = AccessSubject.User(subjectId.ToString("D"));
        AccessScope workspaceScope = WorkspaceAccessScopes.Create(workspaceId);
        await roles.EnsureRoleAsync(
            new(
                WorkspaceAccessRoles.LegacyMember,
                WorkspaceAccessRoles.LegacyMemberPermissions),
            CancellationToken.None).ConfigureAwait(false);
        if (present)
        {
            await roles.EnsureAssignmentAsync(
                subject,
                WorkspaceAccessRoles.LegacyMember,
                workspaceScope,
                CancellationToken.None).ConfigureAwait(false);
            return;
        }

        AccessControlAssignmentRemovalOutcome removed =
            await roles.RemoveAssignmentAsync(
                subject,
                WorkspaceAccessRoles.LegacyMember,
                workspaceScope,
                CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(AccessControlAssignmentRemovalOutcome.Removed, removed);
    }

    private static async Task SuspendMembershipAsync(
        AuthTestApplication api,
        Guid organizationId,
        Guid subjectId)
    {
        using IServiceScope scope = api.Services.CreateScope();
        OrganizationsDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        OrganizationMembership membership = await dbContext.Memberships
            .SingleAsync(candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.SubjectId == subjectId.ToString("D"))
            .ConfigureAwait(false);
        Result suspended = membership.Suspend(
            membership.Version,
            "test:workspace-recovery-membership-drift",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow);
        Assert.True(suspended.IsSuccess, suspended.Error.Code);
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private sealed record ExternalProofCoordinates(
        long OrganizationRevision,
        long StaffTenantRevision,
        long AccessManagementRevision,
        string StaffStateProofSha256,
        string AccessFactsSha256);

    private sealed class StaffProofRevisionBarrierInterceptor
        : DbCommandInterceptor
    {
        private readonly TaskCompletionSource reached = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed;
        private int intercepted;

        public Task Reached => this.reached.Task;

        public void Arm() => Volatile.Write(ref this.armed, 1);

        public void Release() => this.release.TrySetResult();

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref this.armed) == 1 &&
                command.CommandText.Contains(
                    "member_mutation_operations",
                    StringComparison.Ordinal) &&
                Interlocked.CompareExchange(
                    ref this.intercepted,
                    1,
                    0) == 0)
            {
                this.reached.TrySetResult();
                await this.release.Task.WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return await base.ReaderExecutedAsync(
                command,
                eventData,
                result,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<Result<ScopedAccessProfileAssignmentReconciliationDetails>>
        ProbeManagerReconciliationAsync(
            AuthTestApplication api,
            string workspaceId,
            Guid subjectId,
            Guid profileId,
            IReadOnlyCollection<Guid> propertyIds)
    {
        using IServiceScope scope = api.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContextAccessor>().SetTenant(workspaceId);
        AccessScope ownerScope = WorkspaceAccessScopes.Create(workspaceId);
        AccessProfileAssignmentTarget[] targets = propertyIds
            .Distinct()
            .Order()
            .Select(propertyId => new AccessProfileAssignmentTarget(
                profileId,
                WorkspaceAccessScopes.CreateProperty(workspaceId, propertyId)))
            .ToArray();
        return await scope.ServiceProvider.GetRequiredService<IRequestDispatcher>().SendAsync(
            new ReconcileScopedAccessProfileAssignmentsCommand(
                AccessSubject.User(subjectId.ToString("D")),
                ownerScope,
                targets,
                AccessSubject.System(WorkspaceAccessActors.Provisioner)),
            CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task<string> DescribeMessagingStateAsync(
        AuthTestApplication api,
        Guid organizationId)
    {
        using IServiceScope scope = api.Services.CreateScope();
        string scopeId = organizationId.ToString("D");
        OrganizationsDbContext organizations = scope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        WorkspacesDbContext workspaces = scope.ServiceProvider
            .GetRequiredService<WorkspacesDbContext>();

        var outbox = await organizations.OutboxMessages
            .AsNoTracking()
            .Where(message => message.ScopeId == scopeId)
            .OrderByDescending(message => message.CreatedAtUtc)
            .Take(12)
            .Select(message => new
            {
                message.Id,
                message.EventType,
                message.ProcessedAtUtc,
                message.Attempts,
                message.Error
            })
            .ToArrayAsync()
            .ConfigureAwait(false);
        var inbox = await workspaces.InboxMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(message => message.ScopeId == scopeId)
            .OrderByDescending(message => message.CreatedAtUtc)
            .Take(12)
            .Select(message => new
            {
                message.Id,
                message.Handler,
                message.Status,
                message.Attempts,
                message.LastError
            })
            .ToArrayAsync()
            .ConfigureAwait(false);

        string outboxState = outbox.Length == 0
            ? "none"
            : string.Join(
                "; ",
                outbox.Select(message =>
                    $"{message.EventType}/{message.Id:D}:" +
                    $"processed={message.ProcessedAtUtc is not null}," +
                    $"attempts={message.Attempts},error={message.Error ?? "none"}"));
        string inboxState = inbox.Length == 0
            ? "none"
            : string.Join(
                "; ",
                inbox.Select(message =>
                    $"{message.Handler}/{message.Id:D}:" +
                    $"status={message.Status},attempts={message.Attempts}," +
                    $"error={message.LastError ?? "none"}"));
        return $"Organizations outbox: [{outboxState}]. " +
            $"Workspaces inbox: [{inboxState}].";
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

    private static async Task AssertMemberAccessAsync(
        HttpClient client,
        string workspaceId,
        string ownerToken,
        Guid subjectId,
        string profileKey,
        Guid[] propertyIds)
    {
        using HttpResponseMessage response = await SendAsync(
            client,
            HttpMethod.Get,
            $"/api/workspace-access/members/{subjectId:D}/access",
            workspaceId,
            ownerToken).ConfigureAwait(false);
        WorkspaceMemberAccessDto access = await ReadSuccessAsync<WorkspaceMemberAccessDto>(response)
            .ConfigureAwait(false);
        WorkspaceMemberAccessAssignmentDto[] assignments = access.Assignments
            .OrderBy(assignment => assignment.PropertyId)
            .ToArray();
        Assert.Equal(propertyIds.Length, assignments.Length);
        Assert.All(assignments, assignment => Assert.Equal(profileKey, assignment.ProfileKey));
        Assert.Equal(
            propertyIds.Order(),
            assignments.Select(assignment => Assert.IsType<Guid>(assignment.PropertyId)).Order());
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
        builder.Services.AddGmaAccessControlPermissionPolicies(RetentionModuleMetadata.Descriptor);
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
