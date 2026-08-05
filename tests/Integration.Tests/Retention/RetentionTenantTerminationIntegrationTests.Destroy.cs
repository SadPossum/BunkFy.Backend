namespace Integration.Tests.Retention;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Retention.Application.Ports;
using BunkFy.Modules.Retention.Contracts;
using BunkFy.Modules.Retention.Domain.Aggregates;
using BunkFy.Modules.Retention.Domain.Models;
using BunkFy.Modules.Retention.Persistence;
using BunkFy.Modules.Workspaces.Domain.Termination;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed partial class RetentionTenantTerminationIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Tenant_destroy_is_bounded_immutable_and_isolated()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_retention_destroy_tests")
                .Build();
        await postgreSql.StartAsync().ConfigureAwait(false);

        using ServiceProvider tenantAProvider = CreateProvider(
            postgreSql.GetConnectionString(),
            TenantA);
        using (IServiceScope migrationScope =
            tenantAProvider.CreateScope())
        {
            await migrationScope.ServiceProvider
                .GetRequiredService<WorkspacesDbContext>()
                .Database.MigrateAsync()
                .ConfigureAwait(false);
            await migrationScope.ServiceProvider
                .GetRequiredService<RetentionDbContext>()
                .Database.MigrateAsync()
                .ConfigureAwait(false);
        }

        await SeedAsync(
            tenantAProvider,
            TenantA,
            PropertyA,
            ExecutionA).ConfigureAwait(false);
        await SeedDenseStateAsync(tenantAProvider).ConfigureAwait(false);
        using ServiceProvider tenantBProvider = CreateProvider(
            postgreSql.GetConnectionString(),
            TenantB);
        await SeedAsync(
            tenantBProvider,
            TenantB,
            PropertyB,
            ExecutionB).ConfigureAwait(false);

        using IServiceScope inFlightScope = tenantAProvider.CreateScope();
        RetentionDbContext inFlight = inFlightScope.ServiceProvider
            .GetRequiredService<RetentionDbContext>();
        await using IDbContextTransaction inFlightTransaction =
            await inFlight.Database.BeginTransactionAsync()
                .ConfigureAwait(false);
        RetentionTenantProjection projection =
            await inFlight.TenantProjections.SingleAsync()
                .ConfigureAwait(false);
        projection.Apply(
            projection.OrganizationId,
            isActive: true,
            sourceVersion: 2);
        await inFlight.SaveChangesAsync().ConfigureAwait(false);

        WorkspaceTerminationFence fence = CreateTerminationFence();
        using (IServiceScope fenceScope = tenantAProvider.CreateScope())
        {
            WorkspacesDbContext workspaces = fenceScope.ServiceProvider
                .GetRequiredService<WorkspacesDbContext>();
            workspaces.WorkspaceTerminationFences.Add(fence);
            Task<int> persistFence = workspaces.SaveChangesAsync();
            await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);
            Assert.False(persistFence.IsCompleted);
            await inFlightTransaction.CommitAsync().ConfigureAwait(false);
            await persistFence.ConfigureAwait(false);
        }

        using IServiceScope ownerScope = tenantAProvider.CreateScope();
        ITenantTerminationContributor contributor = ownerScope.ServiceProvider
            .GetServices<ITenantTerminationContributor>()
            .Single(candidate => candidate.Descriptor.OwnerKey ==
                RetentionTenantTerminationMetadata.OwnerKey);
        TenantTerminationContributionRequest request = DestroyRequest(fence);
        TenantTerminationContributionResult result =
            await contributor.ExecuteAsync(request, CancellationToken.None)
                .ConfigureAwait(false);
        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal(1, result.AffectedCount);

        using (IServiceScope schedulingScope = tenantAProvider.CreateScope())
        {
            IRetentionScopeRepository scopes = schedulingScope.ServiceProvider
                .GetRequiredService<IRetentionScopeRepository>();
            IReadOnlyList<RetentionScheduleTarget> targets =
                await scopes.ListActiveTargetsAsync(
                    RetentionTargetScopeKind.Property,
                    CancellationToken.None).ConfigureAwait(false);
            RetentionScheduleTarget remaining = Assert.Single(targets);
            Assert.Equal(TenantB, remaining.ScopeId);
            Assert.Equal(PropertyB, remaining.PropertyId);
        }

        result = await contributor.ExecuteAsync(
            request,
            CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal(2, result.AffectedCount);

        result = await contributor.ExecuteAsync(
            request,
            CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal(502, result.AffectedCount);
        using (IServiceScope boundaryScope = tenantAProvider.CreateScope())
        {
            RetentionDbContext boundary = boundaryScope.ServiceProvider
                .GetRequiredService<RetentionDbContext>();
            Assert.Equal(
                1,
                await ReadExecutionCountAsync(boundary, TenantA)
                    .ConfigureAwait(false));
        }

        int attempts = 3;
        while (result.Status ==
                TenantTerminationContributionStatus.RetryRequired &&
            attempts < 20)
        {
            result = await contributor.ExecuteAsync(
                request,
                CancellationToken.None).ConfigureAwait(false);
            attempts++;
        }

        Assert.Equal(
            TenantTerminationContributionStatus.Completed,
            result.Status);
        Assert.Equal("retention.termination.destroyed", result.ResultCode);
        Assert.Equal(505, result.AffectedCount);
        Assert.Equal(3, result.SelectedProofRevision);
        Assert.Equal(4, result.ResultingProofRevision);

        TenantTerminationContributionResult replay =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(result, replay);
        TenantTerminationContributionResult conflict =
            await contributor.ExecuteAsync(
                request with { ExecutingActorId = "other:executor" },
                CancellationToken.None).ConfigureAwait(false);
        Assert.Equal(
            TenantTerminationContributionStatus.Failed,
            conflict.Status);
        Assert.Equal(
            "retention.termination.destroy-conflict",
            conflict.ResultCode);

        using (IServiceScope verificationScope = tenantAProvider.CreateScope())
        {
            RetentionDbContext verification = verificationScope.ServiceProvider
                .GetRequiredService<RetentionDbContext>();
            Assert.Equal(
                0,
                await ReadOwnerRecordCountAsync(verification, TenantA)
                    .ConfigureAwait(false));
            Assert.Equal(
                0,
                await ReadOperationCountAsync(verification, TenantA)
                    .ConfigureAwait(false));
            Assert.Equal(
                1,
                await ReadReceiptCountAsync(verification, TenantA)
                    .ConfigureAwait(false));
            Assert.Equal(
                505,
                await ReadReceiptRemovedCountAsync(verification, TenantA)
                    .ConfigureAwait(false));
            Assert.Equal(
                3,
                await ReadLifecycleStatusAsync(verification, TenantA)
                    .ConfigureAwait(false));

            NpgsqlException immutable =
                await Assert.ThrowsAnyAsync<NpgsqlException>(() =>
                    verification.Database.ExecuteSqlInterpolatedAsync($"""
                        UPDATE retention.tenant_destroy_receipts
                        SET "CompletedBatchCount" = "CompletedBatchCount"
                        WHERE "ScopeId" = {TenantA}
                        """)).ConfigureAwait(false);
            Assert.Contains("append-only", immutable.Message);

            verification.ChangeTracker.Clear();
            verification.PropertyProjections.Add(new(
                TenantA,
                Guid.NewGuid(),
                isActive: true,
                topologySourceVersion: 1));
            InvalidOperationException closed =
                await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                    verification.SaveChangesAsync()).ConfigureAwait(false);
            Assert.Equal(
                "The workspace is not accepting Retention mutations.",
                closed.Message);
        }

        using (IServiceScope tenantBVerificationScope =
            tenantBProvider.CreateScope())
        {
            RetentionDbContext tenantBVerification =
                tenantBVerificationScope.ServiceProvider
                    .GetRequiredService<RetentionDbContext>();
            Assert.Equal(
                4,
                await ReadOwnerRecordCountAsync(
                    tenantBVerification,
                    TenantB).ConfigureAwait(false));
            Assert.Equal(
                1,
                await ReadTenantRevisionAsync(
                    tenantBVerification,
                    TenantB).ConfigureAwait(false));
        }
    }

    private static async Task SeedDenseStateAsync(IServiceProvider services)
    {
        using IServiceScope scope = services.CreateScope();
        RetentionDbContext context = scope.ServiceProvider
            .GetRequiredService<RetentionDbContext>();
        for (int index = 0; index < 500; index++)
        {
            byte[] idBytes = new byte[16];
            BitConverter.GetBytes(index + 1).CopyTo(idBytes, 0);
            idBytes[15] = 0x7f;
            RetentionExecution execution = RetentionExecution.Start(
                new Guid(idBytes),
                TenantA,
                "ingestion",
                "dense-proof",
                RetentionExecutionTargetKind.Tenant,
                propertyId: null,
                executionPolicyVersion: 1,
                attempt: 1,
                ExportNowUtc.AddDays(-2),
                ExportNowUtc.AddDays(-2).AddMinutes(10)).Value;
            context.Executions.Add(execution);
        }

        context.InboxMessages.Add(InboxMessage.Create(
            Guid.Parse("a0000000-0000-0000-0000-000000000001"),
            "retention-integration-handler",
            "bunkfy.retention.integration.v1",
            "retention-integration-observed",
            version: 1,
            TenantA,
            ExportNowUtc.AddMinutes(-5),
            ExportNowUtc.AddMinutes(-5)));
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static TenantTerminationContributionRequest DestroyRequest(
        WorkspaceTerminationFence fence) =>
        new(
            TenantTerminationContract.CurrentVersion,
            TenantA,
            fence.ProcessId,
            fence.CaseId,
            fence.ApprovalRevision,
            OperationRevision: 2,
            fence.TerminationEpoch,
            TenantTerminationContributionPhase.Destroy,
            Guid.Parse("b0000000-0000-0000-0000-000000000001"),
            Guid.Parse("c0000000-0000-0000-0000-000000000001"),
            fence.PolicyEvidenceSha256,
            "termination-executor",
            ExportNowUtc.AddMinutes(30));

    private static Task<long> ReadExecutionCountAsync(
        RetentionDbContext context,
        string tenantId) =>
        context.Database.SqlQuery<long>($"""
            SELECT COUNT(*) AS "Value"
            FROM retention.executions
            WHERE "ScopeId" = {tenantId}
            """).SingleAsync();

    private static Task<long> ReadOwnerRecordCountAsync(
        RetentionDbContext context,
        string tenantId) =>
        context.Database.SqlQuery<long>($"""
            SELECT (
                (SELECT COUNT(*) FROM retention.inbox_messages
                    WHERE "ScopeId" = {tenantId}) +
                (SELECT COUNT(*) FROM retention.schedule_state
                    WHERE "ScopeId" = {tenantId}) +
                (SELECT COUNT(*) FROM retention.executions
                    WHERE "ScopeId" = {tenantId}) +
                (SELECT COUNT(*) FROM retention.property_projection
                    WHERE "ScopeId" = {tenantId}) +
                (SELECT COUNT(*) FROM retention.tenant_projection
                    WHERE "ScopeId" = {tenantId})
            ) AS "Value"
            """).SingleAsync();

    private static Task<long> ReadOperationCountAsync(
        RetentionDbContext context,
        string tenantId) =>
        context.Database.SqlQuery<long>($"""
            SELECT COUNT(*) AS "Value"
            FROM retention.tenant_destroy_operations
            WHERE "ScopeId" = {tenantId}
            """).SingleAsync();

    private static Task<long> ReadReceiptCountAsync(
        RetentionDbContext context,
        string tenantId) =>
        context.Database.SqlQuery<long>($"""
            SELECT COUNT(*) AS "Value"
            FROM retention.tenant_destroy_receipts
            WHERE "ScopeId" = {tenantId}
            """).SingleAsync();

    private static Task<long> ReadReceiptRemovedCountAsync(
        RetentionDbContext context,
        string tenantId) =>
        context.Database.SqlQuery<long>($"""
            SELECT "RemovedRecordCount" AS "Value"
            FROM retention.tenant_destroy_receipts
            WHERE "ScopeId" = {tenantId}
            """).SingleAsync();

    private static Task<int> ReadLifecycleStatusAsync(
        RetentionDbContext context,
        string tenantId) =>
        context.Database.SqlQuery<int>($"""
            SELECT "LifecycleStatus" AS "Value"
            FROM retention.tenant_revisions
            WHERE "ScopeId" = {tenantId}
            """).SingleAsync();
}
