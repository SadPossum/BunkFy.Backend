namespace Integration.Tests;

using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Persistence;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Application.Events.Infrastructure;
using Gma.Framework.Cqrs;
using Gma.Framework.Cqrs.Infrastructure;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Results;
using Gma.Framework.Scoping;
using Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed class StaffMemberMutationOperationIntegrationTests
{
    private const string TenantId =
        "a9000000-0000-0000-0000-000000000001";
    private const string OtherTenantId =
        "a9000000-0000-0000-0000-000000000002";

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Owner_identity_bootstrap_converges_under_exact_replay_and_competing_operations()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_staff_identity_bootstrap_tests")
                .Build();
        await postgreSql.StartAsync().ConfigureAwait(false);

        MutableScopeContext scopeContext = new(TenantId);
        await using ServiceProvider services = CreateProvider(
            postgreSql.GetConnectionString(),
            scopeContext);
        await MigrateAsync(services).ConfigureAwait(false);

        Guid replayOperationId = Guid.NewGuid();
        const string replaySubjectId = "account-owner-replay";
        BootstrapStaffIdentityCommand replay = new(
            replayOperationId,
            replaySubjectId,
            "Workspace Owner",
            "owner-replay@example.test",
            "integration:organizations");

        Result<Unit>[] replayResults = await Task.WhenAll(
            SendAsync(services, replay),
            SendAsync(services, replay)).ConfigureAwait(false);
        Assert.All(
            replayResults,
            result => Assert.True(result.IsSuccess, result.Error.Code));

        Guid competingOperationIdA = Guid.NewGuid();
        Guid competingOperationIdB = Guid.NewGuid();
        const string competingSubjectId = "account-owner-competing";
        Result<Unit>[] competingResults = await Task.WhenAll(
            SendAsync(
                services,
                new BootstrapStaffIdentityCommand(
                    competingOperationIdA,
                    competingSubjectId,
                    "Competing Owner A",
                    "owner-competing@example.test",
                    "integration:organizations")),
            SendAsync(
                services,
                new BootstrapStaffIdentityCommand(
                    competingOperationIdB,
                    competingSubjectId,
                    "Competing Owner B",
                    "owner-competing@example.test",
                    "integration:organizations"))).ConfigureAwait(false);
        Assert.All(
            competingResults,
            result => Assert.True(result.IsSuccess, result.Error.Code));

        using IServiceScope scope = services.CreateScope();
        StaffDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        StaffMember[] members = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(member =>
                member.AuthSubjectId == replaySubjectId ||
                member.AuthSubjectId == competingSubjectId)
            .OrderBy(member => member.AuthSubjectId)
            .ToArrayAsync()
            .ConfigureAwait(false);

        Assert.Equal(2, members.Length);
        StaffMember replayMember = Assert.Single(
            members,
            member => member.AuthSubjectId == replaySubjectId);
        Assert.Equal(replayOperationId, replayMember.Id);

        StaffMember competingMember = Assert.Single(
            members,
            member => member.AuthSubjectId == competingSubjectId);
        Assert.Contains(
            competingMember.Id,
            new[] { competingOperationIdA, competingOperationIdB });

        OutboxMessage[] createdMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message => message.EventType.Contains(
                nameof(StaffMemberCreatedIntegrationEvent)))
            .ToArrayAsync()
            .ConfigureAwait(false);
        Assert.Single(
            createdMessages,
            message => message.Payload.Contains(
                replaySubjectId,
                StringComparison.Ordinal));
        Assert.Single(
            createdMessages,
            message => message.Payload.Contains(
                competingSubjectId,
                StringComparison.Ordinal));
    }

    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Member_mutation_receipts_preserve_legacy_data_and_are_atomic_replayable_scoped_and_immutable()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_staff_member_mutation_operation_tests")
                .Build();
        await postgreSql.StartAsync().ConfigureAwait(false);

        MutableScopeContext scopeContext = new(TenantId);
        await using ServiceProvider services = CreateProvider(
            postgreSql.GetConnectionString(),
            scopeContext);
        await MigrateAsync(
            services,
            "20260807141454_AddStaffProfileUpdateOperations")
            .ConfigureAwait(false);

        Guid staffMemberId = Guid.NewGuid();
        Result<StaffDirectoryMemberDto> created = await SendAsync(
            services,
            new CreateStaffMemberCommand(
                staffMemberId,
                "Maya Chen",
                "Maya Q. Chen",
                "maya@example.test",
                "+44 20 1234 5678",
                "EMP-42",
                "Manager",
                "Operations",
                "account-maya",
                "user:operator")).ConfigureAwait(false);
        Assert.True(created.IsSuccess, created.Error.Code);

        Guid legacyOperationId = Guid.NewGuid();
        await ExecuteSqlAsync(
            postgreSql.GetConnectionString(),
            $$"""
            INSERT INTO "staff"."profile_update_operations"
                ("Id", "ScopeId", "StaffMemberId", "ExpectedVersion",
                 "RequestFingerprint", "ResultStatus", "ResultVersion",
                 "CompletedAtUtc")
            VALUES
                ('{{legacyOperationId:D}}', '{{TenantId}}',
                 '{{staffMemberId:D}}', {{created.Value.Version}},
                 '{{new string('a', 64)}}', 1,
                 {{created.Value.Version}},
                 '2026-08-07T14:30:00Z');
            """).ConfigureAwait(false);
        await MigrateAsync(services).ConfigureAwait(false);
        await AssertOnboardingProvisioningReplayAsync(services)
            .ConfigureAwait(false);

        using (IServiceScope migrationScope = services.CreateScope())
        {
            StaffMemberMutationOperationRecord preserved = Assert.IsType<
                StaffMemberMutationOperationRecord>(
                await migrationScope.ServiceProvider.GetRequiredService<
                        IStaffMemberMutationOperationRepository>()
                    .GetAsync(
                        staffMemberId,
                        legacyOperationId,
                        CancellationToken.None)
                    .ConfigureAwait(false));
            Assert.Equal(
                StaffMemberMutationKind.ProfileUpdate,
                preserved.Kind);
            Assert.Equal(created.Value.Version, preserved.ResultVersion);
        }

        Guid operationId = Guid.NewGuid();
        UpdateStaffMemberCommand update = new(
            operationId,
            staffMemberId,
            "Maya Chen Updated",
            "Maya Q. Chen",
            "MAYA@EXAMPLE.TEST",
            "+44 20 1234 5678",
            "EMP-42",
            "Operations Lead",
            "Operations",
            created.Value.Version,
            "user:operator");
        Result<StaffMemberMutationReceiptDto>[] concurrent =
            await Task.WhenAll(
                SendAsync(services, update),
                SendAsync(services, update)).ConfigureAwait(false);

        Assert.All(
            concurrent,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(concurrent[0].Value, concurrent[1].Value);
        StaffMemberMutationReceiptDto receipt = concurrent[0].Value;
        Assert.Equal(created.Value.Version + 1, receipt.Version);

        Result<StaffMemberMutationReceiptDto> conflictingReuse =
            await SendAsync(
                services,
                update with { DisplayName = "Different reuse" })
                .ConfigureAwait(false);
        Assert.Equal(
            StaffApplicationErrors.ProfileUpdateOperationConflict,
            conflictingReuse.Error);

        Result<StaffMemberMutationReceiptDto> stale = await SendAsync(
            services,
            update with
            {
                OperationId = Guid.NewGuid(),
                DisplayName = "Stale update"
            }).ConfigureAwait(false);
        Assert.Equal(StaffApplicationErrors.VersionConflict, stale.Error);

        Guid authOperationId = Guid.NewGuid();
        SetStaffAuthSubjectCommand authChange = new(
            authOperationId,
            staffMemberId,
            " account-maya-updated ",
            receipt.Version,
            "user:operator");
        Result<StaffMemberMutationReceiptDto>[] concurrentAuthChanges =
            await Task.WhenAll(
                SendAsync(services, authChange),
                SendAsync(services, authChange with
                {
                    AuthSubjectId = "account-maya-updated",
                    ActorId = "user:retrying-operator"
                })).ConfigureAwait(false);
        Assert.All(
            concurrentAuthChanges,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(
            concurrentAuthChanges[0].Value,
            concurrentAuthChanges[1].Value);
        StaffMemberMutationReceiptDto authReceipt =
            concurrentAuthChanges[0].Value;
        Assert.Equal(receipt.Version + 1, authReceipt.Version);

        Result<StaffMemberMutationReceiptDto> crossKindReuse =
            await SendAsync(
                services,
                update with
                {
                    OperationId = authOperationId,
                    ExpectedVersion = authReceipt.Version
                }).ConfigureAwait(false);
        Assert.Equal(
            StaffApplicationErrors.ProfileUpdateOperationConflict,
            crossKindReuse.Error);

        await AssertFailedOutboxWriteRollsBackAsync(
            services,
            postgreSql.GetConnectionString(),
            staffMemberId,
            authReceipt.Version).ConfigureAwait(false);
        await AssertFailedAuthSubjectOutboxWriteRollsBackAsync(
            services,
            postgreSql.GetConnectionString(),
            staffMemberId,
            authReceipt.Version).ConfigureAwait(false);

        Guid propertyId = Guid.NewGuid();
        await SeedActivePropertyAsync(services, propertyId).ConfigureAwait(false);
        Guid assignOperationId = Guid.NewGuid();
        AssignStaffPropertyCommand assign = new(
            assignOperationId,
            staffMemberId,
            propertyId,
            " Duty Manager ",
            true,
            new DateOnly(2026, 8, 7),
            authReceipt.Version,
            "user:operator");
        Result<StaffMemberMutationReceiptDto>[] concurrentAssignments =
            await Task.WhenAll(
                SendAsync(services, assign),
                SendAsync(services, assign with
                {
                    PropertyJobTitle = "Duty Manager",
                    ActorId = "user:retrying-operator"
                })).ConfigureAwait(false);
        Assert.All(
            concurrentAssignments,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(
            concurrentAssignments[0].Value,
            concurrentAssignments[1].Value);
        StaffMemberMutationReceiptDto assignReceipt =
            concurrentAssignments[0].Value;
        Assert.Equal(authReceipt.Version + 1, assignReceipt.Version);

        Guid failedPropertyId = Guid.NewGuid();
        await SeedActivePropertyAsync(services, failedPropertyId)
            .ConfigureAwait(false);
        await AssertFailedAssignmentOutboxWriteRollsBackAsync(
            services,
            postgreSql.GetConnectionString(),
            staffMemberId,
            failedPropertyId,
            assignReceipt.Version).ConfigureAwait(false);

        Guid unassignOperationId = Guid.NewGuid();
        UnassignStaffPropertyCommand unassign = new(
            unassignOperationId,
            staffMemberId,
            propertyId,
            new DateOnly(2026, 8, 7),
            " Transferred ",
            assignReceipt.Version,
            "user:operator");
        Result<StaffMemberMutationReceiptDto>[] concurrentUnassignments =
            await Task.WhenAll(
                SendAsync(services, unassign),
                SendAsync(services, unassign with
                {
                    Reason = "Transferred",
                    ActorId = "user:retrying-operator"
                })).ConfigureAwait(false);
        Assert.All(
            concurrentUnassignments,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(
            concurrentUnassignments[0].Value,
            concurrentUnassignments[1].Value);
        StaffMemberMutationReceiptDto unassignReceipt =
            concurrentUnassignments[0].Value;
        Assert.Equal(assignReceipt.Version + 1, unassignReceipt.Version);

        Guid suspendOperationId = Guid.NewGuid();
        SuspendStaffMemberCommand suspend = new(
            suspendOperationId,
            staffMemberId,
            " Approved leave ",
            unassignReceipt.Version,
            "user:operator");
        Result<StaffMemberMutationReceiptDto>[] concurrentSuspensions =
            await Task.WhenAll(
                SendAsync(services, suspend),
                SendAsync(services, suspend with
                {
                    Reason = "Approved leave",
                    ActorId = "user:retrying-operator"
                })).ConfigureAwait(false);
        Assert.All(
            concurrentSuspensions,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(
            concurrentSuspensions[0].Value,
            concurrentSuspensions[1].Value);
        StaffMemberMutationReceiptDto suspendReceipt =
            concurrentSuspensions[0].Value;
        Assert.Equal(StaffStatus.Suspended, suspendReceipt.Status);

        Guid resumeOperationId = Guid.NewGuid();
        ResumeStaffMemberCommand resume = new(
            resumeOperationId,
            staffMemberId,
            "Returned from leave",
            suspendReceipt.Version,
            "user:operator");
        Result<StaffMemberMutationReceiptDto>[] concurrentResumptions =
            await Task.WhenAll(
                SendAsync(services, resume),
                SendAsync(services, resume with
                {
                    ActorId = "user:retrying-operator"
                })).ConfigureAwait(false);
        Assert.All(
            concurrentResumptions,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(
            concurrentResumptions[0].Value,
            concurrentResumptions[1].Value);
        StaffMemberMutationReceiptDto resumeReceipt =
            concurrentResumptions[0].Value;
        Assert.Equal(StaffStatus.Active, resumeReceipt.Status);

        await AssertFailedLifecycleOutboxWriteRollsBackAsync(
            services,
            postgreSql.GetConnectionString(),
            staffMemberId,
            resumeReceipt.Version).ConfigureAwait(false);

        Guid departOperationId = Guid.NewGuid();
        DepartStaffMemberCommand depart = new(
            departOperationId,
            staffMemberId,
            new DateOnly(2026, 8, 7),
            "Contract ended",
            resumeReceipt.Version,
            "user:operator");
        Result<StaffMemberMutationReceiptDto>[] concurrentDepartures =
            await Task.WhenAll(
                SendAsync(services, depart),
                SendAsync(services, depart with
                {
                    ActorId = "user:retrying-operator"
                })).ConfigureAwait(false);
        Assert.All(
            concurrentDepartures,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(
            concurrentDepartures[0].Value,
            concurrentDepartures[1].Value);
        StaffMemberMutationReceiptDto departReceipt =
            concurrentDepartures[0].Value;
        Assert.Equal(StaffStatus.Departed, departReceipt.Status);

        await VerifyOwnerStateAsync(
            services,
            staffMemberId,
            operationId,
            receipt,
            authOperationId,
            authReceipt,
            propertyId,
            assignOperationId,
            assignReceipt,
            unassignOperationId,
            unassignReceipt,
            suspendOperationId,
            suspendReceipt,
            resumeOperationId,
            resumeReceipt,
            departOperationId,
            departReceipt).ConfigureAwait(false);

        scopeContext.ScopeId = OtherTenantId;
        using (IServiceScope isolatedScope = services.CreateScope())
        {
            StaffDbContext isolated = isolatedScope.ServiceProvider
                .GetRequiredService<StaffDbContext>();
            IStaffMemberMutationOperationRepository operations =
                isolatedScope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>();
            Assert.False(await isolated.StaffMembers.AnyAsync()
                .ConfigureAwait(false));
            Assert.Null(await operations.GetAsync(
                staffMemberId,
                operationId,
                CancellationToken.None).ConfigureAwait(false));
            Assert.Null(await operations.GetAsync(
                staffMemberId,
                authOperationId,
                CancellationToken.None).ConfigureAwait(false));
            Assert.Null(await operations.GetAsync(
                staffMemberId,
                assignOperationId,
                CancellationToken.None).ConfigureAwait(false));
            Assert.Null(await operations.GetAsync(
                staffMemberId,
                unassignOperationId,
                CancellationToken.None).ConfigureAwait(false));
            Assert.Null(await operations.GetAsync(
                staffMemberId,
                suspendOperationId,
                CancellationToken.None).ConfigureAwait(false));
            Assert.Null(await operations.GetAsync(
                staffMemberId,
                resumeOperationId,
                CancellationToken.None).ConfigureAwait(false));
            Assert.Null(await operations.GetAsync(
                staffMemberId,
                departOperationId,
                CancellationToken.None).ConfigureAwait(false));
        }

        scopeContext.ScopeId = TenantId;
        await AssertDirectUpdateRejectedAsync(
            postgreSql.GetConnectionString(),
            staffMemberId,
            operationId).ConfigureAwait(false);
        await DeleteAndVerifyAsync(
            services,
            staffMemberId,
            legacyOperationId,
            operationId,
            authOperationId,
            assignOperationId,
            unassignOperationId,
            suspendOperationId,
            resumeOperationId,
            departOperationId).ConfigureAwait(false);
    }

    private static async Task AssertOnboardingProvisioningReplayAsync(
        ServiceProvider services)
    {
        Guid operationId = Guid.NewGuid();
        ProvisionStaffOnboardingCommand command = new(
            operationId,
            "account-onboarding",
            "Onboarding Applicant",
            "Original Applicant",
            "applicant@example.test",
            null,
            "EMP-ONBOARDING",
            "Receptionist",
            "Operations",
            "integration:organizations",
            "Workspace Staff onboarding accepted.");

        Result<StaffMemberDto>[] concurrent = await Task.WhenAll(
            SendAsync(services, command),
            SendAsync(services, command)).ConfigureAwait(false);
        Assert.All(
            concurrent,
            result => Assert.True(result.IsSuccess, result.Error.Code));
        Assert.Equal(
            concurrent[0].Value.StaffMemberId,
            concurrent[1].Value.StaffMemberId);
        Assert.Equal(concurrent[0].Value.Version, concurrent[1].Value.Version);

        StaffMemberDto provisioned = concurrent[0].Value;
        Result<StaffMemberMutationReceiptDto> edited = await SendAsync(
            services,
            new UpdateStaffMemberCommand(
                Guid.NewGuid(),
                provisioned.StaffMemberId,
                "Operator Edited Applicant",
                provisioned.LegalName,
                provisioned.WorkEmail,
                provisioned.WorkPhone,
                provisioned.EmployeeNumber,
                provisioned.JobTitle,
                provisioned.Department,
                provisioned.Version,
                "user:operator")).ConfigureAwait(false);
        Assert.True(edited.IsSuccess, edited.Error.Code);

        Result<StaffMemberDto> replayed = await SendAsync(
            services,
            command).ConfigureAwait(false);
        Result<StaffMemberDto> conflicting = await SendAsync(
            services,
            command with { DisplayName = "Changed applicant" })
            .ConfigureAwait(false);

        Assert.True(replayed.IsSuccess, replayed.Error.Code);
        Assert.Equal("Operator Edited Applicant", replayed.Value.DisplayName);
        Assert.Equal(edited.Value.Version, replayed.Value.Version);
        Assert.Equal(
            StaffApplicationErrors.OnboardingOperationConflict,
            conflicting.Error);

        Result<StaffMemberMutationReceiptDto> suspended = await SendAsync(
            services,
            new SuspendStaffMemberCommand(
                Guid.NewGuid(),
                provisioned.StaffMemberId,
                "Approved leave",
                edited.Value.Version,
                "user:operator")).ConfigureAwait(false);
        Assert.True(suspended.IsSuccess, suspended.Error.Code);

        Result<StaffMemberDto> replayedAfterSuspension = await SendAsync(
            services,
            command).ConfigureAwait(false);
        Assert.Equal(
            StaffApplicationErrors.OnboardingReplayUnavailable,
            replayedAfterSuspension.Error);

        using IServiceScope scope = services.CreateScope();
        StaffMemberMutationOperationRecord receipt = Assert.IsType<
            StaffMemberMutationOperationRecord>(
            await scope.ServiceProvider.GetRequiredService<
                    IStaffOnboardingProvisioningOperationRepository>()
                .GetAsync(operationId, CancellationToken.None)
                .ConfigureAwait(false));
        Assert.Equal(provisioned.StaffMemberId, receipt.StaffMemberId);
        Assert.Equal(provisioned.Version, receipt.ExpectedVersion);
        Assert.Equal(provisioned.Version, receipt.ResultVersion);
    }

    private static async Task VerifyOwnerStateAsync(
        ServiceProvider services,
        Guid staffMemberId,
        Guid operationId,
        StaffMemberMutationReceiptDto receipt,
        Guid authOperationId,
        StaffMemberMutationReceiptDto authReceipt,
        Guid propertyId,
        Guid assignOperationId,
        StaffMemberMutationReceiptDto assignReceipt,
        Guid unassignOperationId,
        StaffMemberMutationReceiptDto unassignReceipt,
        Guid suspendOperationId,
        StaffMemberMutationReceiptDto suspendReceipt,
        Guid resumeOperationId,
        StaffMemberMutationReceiptDto resumeReceipt,
        Guid departOperationId,
        StaffMemberMutationReceiptDto departReceipt)
    {
        using IServiceScope scope = services.CreateScope();
        StaffDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        StaffMember member = await dbContext.StaffMembers
            .AsNoTracking()
            .Include(candidate => candidate.Assignments)
            .SingleAsync(candidate => candidate.Id == staffMemberId)
            .ConfigureAwait(false);
        StaffMemberMutationOperationRecord operation = Assert.IsType<
            StaffMemberMutationOperationRecord>(
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    operationId,
                    CancellationToken.None)
                .ConfigureAwait(false));
        StaffMemberMutationOperationRecord authOperation = Assert.IsType<
            StaffMemberMutationOperationRecord>(
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    authOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false));
        StaffMemberMutationOperationRecord suspendOperation = Assert.IsType<
            StaffMemberMutationOperationRecord>(
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    suspendOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false));
        StaffMemberMutationOperationRecord assignOperation = Assert.IsType<
            StaffMemberMutationOperationRecord>(
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    assignOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false));
        StaffMemberMutationOperationRecord unassignOperation = Assert.IsType<
            StaffMemberMutationOperationRecord>(
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    unassignOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false));
        StaffMemberMutationOperationRecord resumeOperation = Assert.IsType<
            StaffMemberMutationOperationRecord>(
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    resumeOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false));
        StaffMemberMutationOperationRecord departOperation = Assert.IsType<
            StaffMemberMutationOperationRecord>(
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    departOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false));

        Assert.Equal("Maya Chen Updated", member.DisplayName);
        Assert.Equal("account-maya-updated", member.AuthSubjectId);
        Assert.Equal(StaffMemberState.Departed, member.Status);
        Assert.Equal(departReceipt.Version, member.Version);
        Assert.Equal(receipt, operation.ToReceipt());
        Assert.Equal(
            StaffMemberMutationKind.ProfileUpdate,
            operation.Kind);
        Assert.Equal(authReceipt, authOperation.ToReceipt());
        Assert.Equal(
            StaffMemberMutationKind.AuthSubjectChange,
            authOperation.Kind);
        Assert.Equal(assignReceipt, assignOperation.ToReceipt());
        Assert.Equal(
            StaffMemberMutationKind.AssignProperty,
            assignOperation.Kind);
        Assert.Equal(unassignReceipt, unassignOperation.ToReceipt());
        Assert.Equal(
            StaffMemberMutationKind.UnassignProperty,
            unassignOperation.Kind);
        var assignment = Assert.Single(member.Assignments);
        Assert.Equal(propertyId, assignment.PropertyId);
        Assert.False(assignment.IsCurrent);
        Assert.Equal(suspendReceipt, suspendOperation.ToReceipt());
        Assert.Equal(StaffMemberMutationKind.Suspend, suspendOperation.Kind);
        Assert.Equal(resumeReceipt, resumeOperation.ToReceipt());
        Assert.Equal(StaffMemberMutationKind.Resume, resumeOperation.Kind);
        Assert.Equal(departReceipt, departOperation.ToReceipt());
        Assert.Equal(StaffMemberMutationKind.Depart, departOperation.Kind);
        List<OutboxMessage> memberOutboxMessages = (await dbContext
                .OutboxMessages
                .AsNoTracking()
                .ToListAsync()
                .ConfigureAwait(false))
            .Where(message => message.Payload.Contains(
                staffMemberId.ToString("D"),
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(
            memberOutboxMessages,
            message => message.EventType.Contains(
                nameof(StaffMemberUpdatedIntegrationEvent),
                StringComparison.Ordinal));
        int assignmentEventCount = memberOutboxMessages.Count(
            message => message.EventType.Contains(
                nameof(StaffPropertyAssignmentChangedIntegrationEvent),
                StringComparison.Ordinal));
        Assert.Equal(2, assignmentEventCount);
        Assert.Single(
            memberOutboxMessages,
            message => message.EventType.Contains(
                nameof(StaffAuthSubjectChangedIntegrationEvent),
                StringComparison.Ordinal));
        int lifecycleEventCount = memberOutboxMessages.Count(
            message => message.EventType.Contains(
                nameof(StaffMemberLifecycleChangedIntegrationEvent),
                StringComparison.Ordinal));
        Assert.Equal(3, lifecycleEventCount);
    }

    private static async Task AssertDirectUpdateRejectedAsync(
        string connectionString,
        Guid staffMemberId,
        Guid operationId)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """
            UPDATE "staff"."member_mutation_operations"
            SET "ResultVersion" = "ResultVersion" + 1
            WHERE "ScopeId" = @scope
              AND "StaffMemberId" = @staffMemberId
              AND "Id" = @operationId
            """,
            connection);
        command.Parameters.AddWithValue("scope", TenantId);
        command.Parameters.AddWithValue("staffMemberId", staffMemberId);
        command.Parameters.AddWithValue("operationId", operationId);

        PostgresException exception = await Assert.ThrowsAsync<
            PostgresException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
        Assert.Contains(
            "immutable",
            exception.MessageText,
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertFailedOutboxWriteRollsBackAsync(
        ServiceProvider services,
        string connectionString,
        Guid staffMemberId,
        long expectedVersion)
    {
        const string functionName =
            "staff.fail_profile_update_outbox_insert";
        await ExecuteSqlAsync(
            connectionString,
            $$"""
            CREATE FUNCTION {{functionName}}()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'Profile update outbox failure';
            END;
            $function$;

            CREATE TRIGGER "TR_staff_profile_update_outbox_failure"
            BEFORE INSERT ON "staff"."outbox_messages"
            FOR EACH ROW
            EXECUTE FUNCTION {{functionName}}();
            """).ConfigureAwait(false);

        Guid failedOperationId = Guid.NewGuid();
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => SendAsync(
                services,
                new UpdateStaffMemberCommand(
                    failedOperationId,
                    staffMemberId,
                    "Must roll back",
                    "Maya Q. Chen",
                    "maya@example.test",
                    "+44 20 1234 5678",
                    "EMP-42",
                    "Operations Lead",
                    "Operations",
                    expectedVersion,
                    "user:operator")));
        }
        finally
        {
            await ExecuteSqlAsync(
                connectionString,
                $$"""
                DROP TRIGGER IF EXISTS
                    "TR_staff_profile_update_outbox_failure"
                    ON "staff"."outbox_messages";
                DROP FUNCTION IF EXISTS {{functionName}}();
                """).ConfigureAwait(false);
        }

        using IServiceScope scope = services.CreateScope();
        StaffMember persisted = await scope.ServiceProvider
            .GetRequiredService<StaffDbContext>()
            .StaffMembers.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == staffMemberId)
            .ConfigureAwait(false);
        StaffMemberMutationOperationRecord? operation =
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    failedOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false);

        Assert.Equal("Maya Chen Updated", persisted.DisplayName);
        Assert.Equal("account-maya-updated", persisted.AuthSubjectId);
        Assert.Equal(expectedVersion, persisted.Version);
        Assert.Null(operation);
    }

    private static async Task AssertFailedAuthSubjectOutboxWriteRollsBackAsync(
        ServiceProvider services,
        string connectionString,
        Guid staffMemberId,
        long expectedVersion)
    {
        const string functionName =
            "staff.fail_auth_subject_outbox_insert";
        await ExecuteSqlAsync(
            connectionString,
            $$"""
            CREATE FUNCTION {{functionName}}()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'Auth-subject outbox failure';
            END;
            $function$;

            CREATE TRIGGER "TR_staff_auth_subject_outbox_failure"
            BEFORE INSERT ON "staff"."outbox_messages"
            FOR EACH ROW
            EXECUTE FUNCTION {{functionName}}();
            """).ConfigureAwait(false);

        Guid failedOperationId = Guid.NewGuid();
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => SendAsync(
                services,
                new SetStaffAuthSubjectCommand(
                    failedOperationId,
                    staffMemberId,
                    "account-must-roll-back",
                    expectedVersion,
                    "user:operator")));
        }
        finally
        {
            await ExecuteSqlAsync(
                connectionString,
                $$"""
                DROP TRIGGER IF EXISTS
                    "TR_staff_auth_subject_outbox_failure"
                    ON "staff"."outbox_messages";
                DROP FUNCTION IF EXISTS {{functionName}}();
                """).ConfigureAwait(false);
        }

        using IServiceScope scope = services.CreateScope();
        StaffMember persisted = await scope.ServiceProvider
            .GetRequiredService<StaffDbContext>()
            .StaffMembers.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == staffMemberId)
            .ConfigureAwait(false);
        StaffMemberMutationOperationRecord? operation =
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    failedOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false);

        Assert.Equal("account-maya-updated", persisted.AuthSubjectId);
        Assert.Equal(expectedVersion, persisted.Version);
        Assert.Null(operation);
    }

    private static async Task AssertFailedAssignmentOutboxWriteRollsBackAsync(
        ServiceProvider services,
        string connectionString,
        Guid staffMemberId,
        Guid propertyId,
        long expectedVersion)
    {
        const string functionName =
            "staff.fail_assignment_outbox_insert";
        await ExecuteSqlAsync(
            connectionString,
            $$"""
            CREATE FUNCTION {{functionName}}()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'Assignment outbox failure';
            END;
            $function$;

            CREATE TRIGGER "TR_staff_assignment_outbox_failure"
            BEFORE INSERT ON "staff"."outbox_messages"
            FOR EACH ROW
            EXECUTE FUNCTION {{functionName}}();
            """).ConfigureAwait(false);

        Guid failedOperationId = Guid.NewGuid();
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => SendAsync(
                services,
                new AssignStaffPropertyCommand(
                    failedOperationId,
                    staffMemberId,
                    propertyId,
                    "Must roll back",
                    false,
                    new DateOnly(2026, 8, 7),
                    expectedVersion,
                    "user:operator")));
        }
        finally
        {
            await ExecuteSqlAsync(
                connectionString,
                $$"""
                DROP TRIGGER IF EXISTS
                    "TR_staff_assignment_outbox_failure"
                    ON "staff"."outbox_messages";
                DROP FUNCTION IF EXISTS {{functionName}}();
                """).ConfigureAwait(false);
        }

        using IServiceScope scope = services.CreateScope();
        StaffMember persisted = await scope.ServiceProvider
            .GetRequiredService<StaffDbContext>()
            .StaffMembers.AsNoTracking()
            .Include(candidate => candidate.Assignments)
            .SingleAsync(candidate => candidate.Id == staffMemberId)
            .ConfigureAwait(false);
        StaffMemberMutationOperationRecord? operation =
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    failedOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false);

        Assert.Equal(expectedVersion, persisted.Version);
        Assert.DoesNotContain(
            persisted.Assignments,
            assignment => assignment.PropertyId == propertyId);
        Assert.Null(operation);
    }

    private static async Task AssertFailedLifecycleOutboxWriteRollsBackAsync(
        ServiceProvider services,
        string connectionString,
        Guid staffMemberId,
        long expectedVersion)
    {
        const string functionName =
            "staff.fail_lifecycle_outbox_insert";
        await ExecuteSqlAsync(
            connectionString,
            $$"""
            CREATE FUNCTION {{functionName}}()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $function$
            BEGIN
                RAISE EXCEPTION 'Lifecycle outbox failure';
            END;
            $function$;

            CREATE TRIGGER "TR_staff_lifecycle_outbox_failure"
            BEFORE INSERT ON "staff"."outbox_messages"
            FOR EACH ROW
            EXECUTE FUNCTION {{functionName}}();
            """).ConfigureAwait(false);

        Guid failedOperationId = Guid.NewGuid();
        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => SendAsync(
                services,
                new SuspendStaffMemberCommand(
                    failedOperationId,
                    staffMemberId,
                    "Must roll back",
                    expectedVersion,
                    "user:operator")));
        }
        finally
        {
            await ExecuteSqlAsync(
                connectionString,
                $$"""
                DROP TRIGGER IF EXISTS
                    "TR_staff_lifecycle_outbox_failure"
                    ON "staff"."outbox_messages";
                DROP FUNCTION IF EXISTS {{functionName}}();
                """).ConfigureAwait(false);
        }

        using IServiceScope scope = services.CreateScope();
        StaffMember persisted = await scope.ServiceProvider
            .GetRequiredService<StaffDbContext>()
            .StaffMembers.AsNoTracking()
            .SingleAsync(candidate => candidate.Id == staffMemberId)
            .ConfigureAwait(false);
        StaffMemberMutationOperationRecord? operation =
            await scope.ServiceProvider.GetRequiredService<
                    IStaffMemberMutationOperationRepository>()
                .GetAsync(
                    staffMemberId,
                    failedOperationId,
                    CancellationToken.None)
                .ConfigureAwait(false);

        Assert.Equal(StaffMemberState.Active, persisted.Status);
        Assert.Equal(expectedVersion, persisted.Version);
        Assert.Null(operation);
    }

    private static async Task DeleteAndVerifyAsync(
        ServiceProvider services,
        Guid staffMemberId,
        params Guid[] operationIds)
    {
        using IServiceScope scope = services.CreateScope();
        IStaffMemberMutationOperationRepository operations =
            scope.ServiceProvider.GetRequiredService<
                IStaffMemberMutationOperationRepository>();
        await operations.DeleteForStaffMemberAsync(
            staffMemberId,
            CancellationToken.None).ConfigureAwait(false);

        foreach (Guid operationId in operationIds)
        {
            Assert.Null(await operations.GetAsync(
                staffMemberId,
                operationId,
                CancellationToken.None).ConfigureAwait(false));
        }
    }

    private static async Task SeedActivePropertyAsync(
        ServiceProvider services,
        Guid propertyId)
    {
        using IServiceScope scope = services.CreateScope();
        StaffDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        dbContext.PropertyProjections.Add(new StaffPropertyProjection(
            TenantId,
            propertyId,
            "Staff House",
            PropertyStatus.Active,
            version: 1));
        await dbContext.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task<Result<TResponse>> SendAsync<TResponse>(
        ServiceProvider services,
        ICommand<TResponse> command)
    {
        using IServiceScope scope = services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<IRequestDispatcher>()
            .SendAsync(command, CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task MigrateAsync(
        ServiceProvider services,
        string? targetMigration = null)
    {
        using IServiceScope scope = services.CreateScope();
        StaffDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<StaffDbContext>();
        await dbContext.Database.GetService<IMigrator>()
            .MigrateAsync(targetMigration)
            .ConfigureAwait(false);
    }

    private static async Task ExecuteSqlAsync(
        string connectionString,
        string commandText)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = new(commandText, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static ServiceProvider CreateProvider(
        string connectionString,
        MutableScopeContext scopeContext)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration["Persistence:Provider"] = "PostgreSql";
        builder.Configuration["ConnectionStrings:PostgreSql"] =
            connectionString;
        builder.Services.AddSingleton<IScopeContext>(scopeContext);
        builder.Services.AddSingleton<IWorkspaceTerminationFenceReader>(
            OpenWorkspaceTerminationFenceReader.Instance);
        builder.AddApplicationEventsInfrastructure();
        builder.AddCqrsInfrastructure();
        builder.AddMessagingInfrastructure();
        builder.Services.AddStaffApplication();
        builder.AddStaffPersistence();
        return builder.Services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });
    }

    private sealed class MutableScopeContext(string scopeId) : IScopeContext
    {
        public bool IsEnabled => true;
        public string? ScopeId { get; set; } = scopeId;
    }
}
