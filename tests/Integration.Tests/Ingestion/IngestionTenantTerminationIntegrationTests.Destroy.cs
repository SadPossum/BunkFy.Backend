namespace Integration.Tests;

using System.Collections.Concurrent;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Controls;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Workspaces.Domain.Termination;
using BunkFy.Modules.Workspaces.Persistence;
using Gma.Framework.Messaging.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

public sealed partial class IngestionTenantTerminationIntegrationTests
{
    [DockerFact]
    [Trait("Category", "Docker")]
    [Trait("Category", "Integration")]
    public async Task Tenant_destroy_is_storage_safe_bounded_immutable_and_isolated()
    {
        await using PostgreSqlContainer postgreSql =
            new PostgreSqlBuilder("postgres:16-alpine")
                .WithDatabase("bunkfy_ingestion_tenant_destroy_tests")
                .Build();
        await postgreSql.StartAsync();
        string connectionString = postgreSql.GetConnectionString();

        TestRawPayloadStore tenantARawPayloads = new();
        TestClock tenantAClock = new(ExportNowUtc);
        using ServiceProvider tenantAProvider = CreatePersistenceProvider(
            connectionString,
            TenantA,
            tenantAClock,
            tenantARawPayloads);
        using (IServiceScope seedScope = tenantAProvider.CreateScope())
        {
            IngestionDbContext ingestion = seedScope.ServiceProvider
                .GetRequiredService<IngestionDbContext>();
            WorkspacesDbContext workspaces = seedScope.ServiceProvider
                .GetRequiredService<WorkspacesDbContext>();
            await ingestion.Database.MigrateAsync();
            await workspaces.Database.MigrateAsync();
            _ = await SeedGraphAsync(seedScope.ServiceProvider, ingestion);
        }

        WorkspaceTerminationFence tenantAFence =
            await AddFenceAsync(tenantAProvider, TenantA);
        using (IServiceScope blockedScope = tenantAProvider.CreateScope())
        {
            ITenantTerminationContributor contributor = ResolveContributor(
                blockedScope.ServiceProvider);
            TenantTerminationContributionResult blocked =
                await contributor.ExecuteAsync(
                    TenantDestroyRequest(
                        tenantAFence,
                        TenantA,
                        Guid.Parse(
                            "a1000000-0000-0000-0000-000000000001")),
                    CancellationToken.None);

            Assert.Equal(
                TenantTerminationContributionStatus.Blocked,
                blocked.Status);
            Assert.Equal(
                "ingestion.termination.destroy-active-hold",
                blocked.ResultCode);
            Assert.Equal(1, blocked.RemainingActiveCount);
        }

        Assert.Equal(
            0,
            await CountForTenantAsync(
                connectionString,
                "ingestion.tenant_destroy_operations",
                TenantA));
        Assert.Empty(tenantARawPayloads.DeletedCoordinates);

        TestRawPayloadStore tenantBRawPayloads = new();
        TestClock tenantBClock = new(ExportNowUtc);
        using ServiceProvider tenantBProvider = CreatePersistenceProvider(
            connectionString,
            TenantB,
            tenantBClock,
            tenantBRawPayloads);
        SeedProof tenantBProof;
        using (IServiceScope seedScope = tenantBProvider.CreateScope())
        {
            IngestionDbContext ingestion = seedScope.ServiceProvider
                .GetRequiredService<IngestionDbContext>();
            tenantBProof = await SeedGraphAsync(
                seedScope.ServiceProvider,
                ingestion);
            LegalHold hold = await ingestion.LegalHolds.SingleAsync(candidate =>
                candidate.State == LegalHoldState.Active);
            Assert.True(hold.Release(
                hold.Version,
                "user:privacy-controller",
                "Workspace termination approved.",
                tenantBClock.UtcNow.AddMinutes(-1)).IsSuccess);
            ingestion.AdapterIngressGlobalControls.Add(
                AdapterIngressGlobalControl.CreateStopped(
                    "incident.test",
                    "admin:operator",
                    tenantBClock.UtcNow.AddMinutes(-1)).Value);
            OutboxMessage[] messages = Enumerable.Range(0, 501)
                .Select(_ => new OutboxMessage(
                    Guid.NewGuid(),
                    "bunkfy.ingestion.termination-test.v1",
                    "termination-test",
                    version: 1,
                    TenantB,
                    tenantBClock.UtcNow,
                    "{}",
                    tenantBClock.UtcNow))
                .ToArray();
            messages[0].MarkClaimed(
                "termination-test-worker",
                tenantBClock.UtcNow,
                TimeSpan.FromMinutes(1));
            ingestion.OutboxMessages.AddRange(messages);
            await ingestion.SaveChangesAsync();
        }

        tenantBRawPayloads.Seed(
            tenantBProof.RawPayloadFileId,
            TenantB,
            tenantBProof.ConnectionId);
        tenantBRawPayloads.Seed(
            tenantBProof.DerivedRawPayloadFileId,
            TenantB,
            tenantBProof.ConnectionId);
        long selectedRevision = await ScalarForTenantAsync(
            connectionString,
            """
            SELECT "Revision"::bigint
            FROM ingestion.tenant_revisions
            WHERE "ScopeId" = @tenantId
            """,
            TenantB);

        WorkspaceTerminationFence tenantBFence =
            await AddFenceAsync(tenantBProvider, TenantB);
        Guid operationId = Guid.Parse(
            "b1000000-0000-0000-0000-000000000001");
        TenantTerminationContributionRequest request = TenantDestroyRequest(
            tenantBFence,
            TenantB,
            operationId);
        using IServiceScope destroyScope = tenantBProvider.CreateScope();
        ITenantTerminationContributor destroyContributor = ResolveContributor(
            destroyScope.ServiceProvider);

        TenantTerminationContributionResult busy =
            await destroyContributor.ExecuteAsync(
                request,
                CancellationToken.None);
        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            busy.Status);
        Assert.Equal(
            "ingestion.termination.destroy-outbox-busy",
            busy.ResultCode);
        Assert.Equal(
            501,
            await CountForTenantAsync(
                connectionString,
                "ingestion.outbox_messages",
                TenantB));

        tenantBClock.UtcNow = tenantBClock.UtcNow.AddMinutes(2);
        tenantBRawPayloads.PreserveOnDelete = true;
        TenantTerminationContributionResult storageRetry =
            await destroyContributor.ExecuteAsync(
                request,
                CancellationToken.None);
        Assert.Equal(
            "ingestion.termination.destroy-raw-payload-retry",
            storageRetry.ResultCode);
        Assert.Equal(0, storageRetry.AffectedCount);
        Assert.True(tenantBRawPayloads.Contains(
            tenantBProof.RawPayloadFileId,
            TenantB,
            tenantBProof.ConnectionId));
        Assert.True(tenantBRawPayloads.Contains(
            tenantBProof.DerivedRawPayloadFileId,
            TenantB,
            tenantBProof.ConnectionId));

        tenantBRawPayloads.PreserveOnDelete = false;
        TenantTerminationContributionResult rawPayloadBatch =
            await destroyContributor.ExecuteAsync(
                request,
                CancellationToken.None);
        Assert.Equal(
            "ingestion.termination.destroy-in-progress",
            rawPayloadBatch.ResultCode);
        Assert.Equal(2, rawPayloadBatch.AffectedCount);
        Assert.False(tenantBRawPayloads.Contains(
            tenantBProof.RawPayloadFileId,
            TenantB,
            tenantBProof.ConnectionId));
        Assert.False(tenantBRawPayloads.Contains(
            tenantBProof.DerivedRawPayloadFileId,
            TenantB,
            tenantBProof.ConnectionId));
        Assert.Equal(
            2,
            await ScalarForTenantAsync(
                connectionString,
                """
                SELECT COUNT(*)
                FROM ingestion.observation_receipts
                WHERE "ScopeId" = @tenantId
                  AND "RawPayloadRetentionState" = 3
                """,
                TenantB));

        TenantTerminationContributionResult firstDatabaseBatch =
            await destroyContributor.ExecuteAsync(
                request,
                CancellationToken.None);
        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            firstDatabaseBatch.Status);
        Assert.Equal(502, firstDatabaseBatch.AffectedCount);
        Assert.Equal(
            1,
            await CountForTenantAsync(
                connectionString,
                "ingestion.outbox_messages",
                TenantB));

        TenantTerminationContributionResult completed = firstDatabaseBatch;
        int attempts = 1;
        while (completed.Status ==
                TenantTerminationContributionStatus.RetryRequired &&
            attempts < 100)
        {
            completed = await destroyContributor.ExecuteAsync(
                request,
                CancellationToken.None);
            attempts++;
        }

        Assert.Equal(
            TenantTerminationContributionStatus.Completed,
            completed.Status);
        Assert.Equal(
            "ingestion.termination.destroyed",
            completed.ResultCode);
        Assert.True(completed.AffectedCount > 502);
        Assert.Equal(selectedRevision, completed.SelectedProofRevision);
        Assert.Equal(
            selectedRevision + 1,
            completed.ResultingProofRevision);
        Assert.Equal(
            0,
            await CountForTenantAsync(
                connectionString,
                "ingestion.tenant_destroy_operations",
                TenantB));
        Assert.Equal(
            1,
            await CountForTenantAsync(
                connectionString,
                "ingestion.tenant_destroy_receipts",
                TenantB));
        Assert.Equal(
            0,
            await CountRemainingOwnerRowsAsync(connectionString, TenantB));
        Assert.Equal(
            3,
            await ScalarForTenantAsync(
                connectionString,
                """
                SELECT "LifecycleStatus"::bigint
                FROM ingestion.tenant_revisions
                WHERE "ScopeId" = @tenantId
                """,
                TenantB));
        Assert.Equal(
            1,
            await ScalarAsync(
                connectionString,
                "SELECT COUNT(*) FROM ingestion.adapter_ingress_global_controls"));

        TenantTerminationContributionResult replay =
            await destroyContributor.ExecuteAsync(
                request,
                CancellationToken.None);
        Assert.Equal(completed, replay);
        TenantTerminationContributionResult conflict =
            await destroyContributor.ExecuteAsync(
                request with { ExecutingActorId = "other:executor" },
                CancellationToken.None);
        Assert.Equal(
            TenantTerminationContributionStatus.Failed,
            conflict.Status);

        IngestionDbContext closedContext = destroyScope.ServiceProvider
            .GetRequiredService<IngestionDbContext>();
        closedContext.ChangeTracker.Clear();
        closedContext.OutboxMessages.Add(new OutboxMessage(
            Guid.NewGuid(),
            "bunkfy.ingestion.termination-test.v1",
            "termination-test",
            version: 1,
            TenantB,
            tenantBClock.UtcNow,
            "{}",
            tenantBClock.UtcNow));
        InvalidOperationException closedFailure =
            await Assert.ThrowsAnyAsync<InvalidOperationException>(
                () => closedContext.SaveChangesAsync());
        Assert.Equal(
            "The workspace is not accepting Ingestion mutations.",
            closedFailure.Message);
        closedContext.ChangeTracker.Clear();

        PostgresException receiptMutation =
            await Assert.ThrowsAsync<PostgresException>(() =>
                closedContext.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE ingestion.tenant_destroy_receipts
                    SET "RemovedRecordCount" = "RemovedRecordCount"
                    WHERE "ScopeId" = {TenantB};
                    """));
        Assert.Equal("P0001", receiptMutation.SqlState);
        Assert.Contains(
            "ingestion anonymisation receipts are append-only",
            receiptMutation.MessageText,
            StringComparison.Ordinal);

        Assert.True(await CountForTenantAsync(
            connectionString,
            "ingestion.adapter_connections",
            TenantA) > 0);
        Assert.Equal(
            1,
            await ScalarForTenantAsync(
                connectionString,
                """
                SELECT "LifecycleStatus"::bigint
                FROM ingestion.tenant_revisions
                WHERE "ScopeId" = @tenantId
                """,
                TenantA));
    }

    private static async Task<WorkspaceTerminationFence> AddFenceAsync(
        IServiceProvider services,
        string tenantId)
    {
        using IServiceScope scope = services.CreateScope();
        WorkspacesDbContext workspaces = scope.ServiceProvider
            .GetRequiredService<WorkspacesDbContext>();
        WorkspaceTerminationFence fence = WorkspaceTerminationFence.Freeze(
            Guid.NewGuid(),
            tenantId,
            Guid.NewGuid(),
            CaseId,
            approvalRevision: 1,
            Guid.NewGuid(),
            Digest,
            "termination-operator",
            FrozenAtUtc).Value;
        workspaces.WorkspaceTerminationFences.Add(fence);
        await workspaces.SaveChangesAsync();
        return fence;
    }

    private static TenantTerminationContributionRequest TenantDestroyRequest(
        WorkspaceTerminationFence fence,
        string tenantId,
        Guid idempotencyKey) =>
        new(
            TenantTerminationContract.CurrentVersion,
            tenantId,
            fence.ProcessId,
            fence.CaseId,
            fence.ApprovalRevision,
            OperationRevision: 2,
            fence.TerminationEpoch,
            TenantTerminationContributionPhase.Destroy,
            Guid.NewGuid(),
            idempotencyKey,
            fence.PolicyEvidenceSha256,
            "termination-executor",
            ExportNowUtc.AddHours(2));

    private static ITenantTerminationContributor ResolveContributor(
        IServiceProvider services) =>
        services.GetServices<ITenantTerminationContributor>()
            .Single(contributor => string.Equals(
                contributor.Descriptor.OwnerKey,
                IngestionTenantTerminationMetadata.OwnerKey,
                StringComparison.Ordinal));

    private static Task<long> CountForTenantAsync(
        string connectionString,
        string qualifiedTable,
        string tenantId) =>
        ScalarForTenantAsync(
            connectionString,
            $"SELECT COUNT(*) FROM {qualifiedTable} " +
            "WHERE \"ScopeId\" = @tenantId",
            tenantId);

    private static Task<long> CountRemainingOwnerRowsAsync(
        string connectionString,
        string tenantId) =>
        ScalarForTenantAsync(
            connectionString,
            """
            SELECT (
                (SELECT COUNT(*) FROM ingestion.outbox_messages WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.inbox_messages WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.adapter_connections WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.adapter_ingress_credentials WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.adapter_ingress_tenant_controls WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.property_projection WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.projection_rebuild_checkpoints WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.runs WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.observation_receipts WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.observation_reprocessing_attempts WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.observation_reprocessing_outputs WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.change_proposals WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.reservation_source_links WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.reservation_dispatches WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.legal_holds WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.retention_executions WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.anonymisation_tombstones WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.anonymisation_receipts WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.anonymisation_fingerprints WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.anonymisation_record_plan WHERE "ScopeId" = @tenantId) +
                (SELECT COUNT(*) FROM ingestion.source_operation_locks WHERE "ScopeId" = @tenantId)
            )::bigint
            """,
            tenantId);

    private static async Task<long> ScalarForTenantAsync(
        string connectionString,
        string commandText,
        string tenantId)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        command.Parameters.AddWithValue("tenantId", tenantId);
        object? value = await command.ExecuteScalarAsync();
        return Convert.ToInt64(
            value,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<long> ScalarAsync(
        string connectionString,
        string commandText)
    {
        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(commandText, connection);
        object? value = await command.ExecuteScalarAsync();
        return Convert.ToInt64(
            value,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed class TestRawPayloadStore : IRawPayloadStore
    {
        private readonly ConcurrentDictionary<(
            Guid PayloadId,
            string ScopeId,
            Guid ConnectionId), byte> payloads = new();

        public bool PreserveOnDelete { get; set; }

        public ConcurrentQueue<(Guid PayloadId, string ScopeId, Guid ConnectionId)>
            DeletedCoordinates
        { get; } = new();

        public void Seed(
            Guid payloadId,
            string scopeId,
            Guid connectionId) =>
            this.payloads.TryAdd((payloadId, scopeId, connectionId), 0);

        public bool Contains(
            Guid payloadId,
            string scopeId,
            Guid connectionId) =>
            this.payloads.ContainsKey((payloadId, scopeId, connectionId));

        public Task StoreAsync(
            RawPayloadWrite write,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.payloads[(
                write.PayloadId,
                write.ScopeId,
                write.ConnectionId)] = 0;
            return Task.CompletedTask;
        }

        public Task<RawPayloadRead?> ReadAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RawPayloadRead? result = this.Contains(
                payloadId,
                scopeId,
                connectionId)
                ? new RawPayloadRead(
                    "application/json",
                    "{}"u8.ToArray(),
                    Digest)
                : null;
            return Task.FromResult(result);
        }

        public Task<bool> DeleteAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.DeletedCoordinates.Enqueue((
                payloadId,
                scopeId,
                connectionId));
            bool removed = !this.PreserveOnDelete &&
                this.payloads.TryRemove(
                    (payloadId, scopeId, connectionId),
                    out _);
            return Task.FromResult(removed);
        }
    }
}
