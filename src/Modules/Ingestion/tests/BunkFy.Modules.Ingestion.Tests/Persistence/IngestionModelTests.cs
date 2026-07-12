namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Runs;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionModelTests
{
    [Fact]
    public void Mutable_aggregates_use_concurrency_tokens()
    {
        using IngestionDbContext dbContext = CreateDbContext();

        Assert.True(dbContext.Model.FindEntityType(typeof(AdapterConnection))!
            .FindProperty(nameof(AdapterConnection.Version))!.IsConcurrencyToken);
        Assert.True(dbContext.Model.FindEntityType(typeof(AdapterConnection))!
            .FindProperty(nameof(AdapterConnection.RemoteLeaseEpoch))!.IsConcurrencyToken);
        Assert.True(dbContext.Model.FindEntityType(typeof(IngestionRun))!
            .FindProperty(nameof(IngestionRun.Version))!.IsConcurrencyToken);
        Assert.True(dbContext.Model.FindEntityType(typeof(ChangeProposal))!
            .FindProperty(nameof(ChangeProposal.Version))!.IsConcurrencyToken);
        Assert.True(dbContext.Model.FindEntityType(typeof(ReservationSourceLink))!
            .FindProperty(nameof(ReservationSourceLink.Version))!.IsConcurrencyToken);
        Assert.True(dbContext.Model.FindEntityType(typeof(ReservationDispatch))!
            .FindProperty(nameof(ReservationDispatch.Version))!.IsConcurrencyToken);
        Assert.True(dbContext.Model.FindEntityType(typeof(ObservationReceipt))!
            .FindProperty(nameof(ObservationReceipt.RawPayloadVersion))!.IsConcurrencyToken);
        Assert.True(dbContext.Model.FindEntityType(typeof(ObservationReprocessingAttempt))!
            .FindProperty(nameof(ObservationReprocessingAttempt.Version))!.IsConcurrencyToken);
        Assert.True(dbContext.Model.FindEntityType(typeof(LegalHold))!
            .FindProperty(nameof(LegalHold.Version))!.IsConcurrencyToken);
        Assert.True(dbContext.Model.FindEntityType(typeof(IngestionPropertyProjection))!
            .FindProperty(nameof(IngestionPropertyProjection.RetentionFenceVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void Reprocessing_attempts_outputs_and_derived_receipts_are_scope_linked()
    {
        using IngestionDbContext dbContext = CreateDbContext();
        IEntityType receipt = dbContext.Model.FindEntityType(typeof(ObservationReceipt))!;
        IEntityType attempt = dbContext.Model.FindEntityType(typeof(ObservationReprocessingAttempt))!;
        IEntityType output = dbContext.Model.FindEntityType(typeof(ObservationReprocessingOutput))!;

        Assert.Contains(attempt.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(["ScopeId", "SourceReceiptId"]));
        Assert.Contains(receipt.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(["ScopeId", "SourceReceiptId"]));
        Assert.Contains(receipt.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "ReprocessingAttemptId"]));
        Assert.Contains(output.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "AttemptId", "OutputIndex"]));
    }

    [Fact]
    public void Receipts_have_operation_and_source_deduplication_barriers()
    {
        using IngestionDbContext dbContext = CreateDbContext();
        IEntityType receipt = dbContext.Model.FindEntityType(typeof(ObservationReceipt))!;

        Assert.Contains(receipt.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "ConnectionId", "OperationId"]));
        Assert.Contains(receipt.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "ConnectionId", "DeduplicationKey"]));
    }

    [Fact]
    public void Run_and_receipt_relationships_include_scope()
    {
        using IngestionDbContext dbContext = CreateDbContext();
        IEntityType run = dbContext.Model.FindEntityType(typeof(IngestionRun))!;
        IEntityType receipt = dbContext.Model.FindEntityType(typeof(ObservationReceipt))!;

        Assert.Contains(run.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(["ScopeId", "ConnectionId"]));
        Assert.Contains(receipt.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(["ScopeId", "RunId"]));
    }

    [Fact]
    public void Task_and_remote_lease_identities_are_unique_and_receipt_run_is_optional()
    {
        using IngestionDbContext dbContext = CreateDbContext();
        IEntityType run = dbContext.Model.FindEntityType(typeof(IngestionRun))!;
        IEntityType receipt = dbContext.Model.FindEntityType(typeof(ObservationReceipt))!;

        Assert.Contains(run.GetIndexes(), index =>
            index.IsUnique && index.GetFilter() == "\"ExecutionKind\" = 1" &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "TaskRunId", "TaskAttempt"]));
        Assert.Contains(run.GetIndexes(), index =>
            index.IsUnique && index.GetFilter() == "\"ExecutionKind\" = 2" &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "RemoteLeaseId"]));
        Assert.True(run.FindProperty(nameof(IngestionRun.TaskRunId))!.IsNullable);
        Assert.True(run.FindProperty(nameof(IngestionRun.TaskAttempt))!.IsNullable);
        Assert.True(receipt.FindProperty(nameof(ObservationReceipt.RunId))!.IsNullable);
    }

    [Fact]
    public void Polling_schedule_is_complete_and_active_runs_are_unique_per_connection()
    {
        using IngestionDbContext dbContext = CreateDbContext();
        IEntityType connection = dbContext.Model.FindEntityType(typeof(AdapterConnection))!;
        IEntityType run = dbContext.Model.FindEntityType(typeof(IngestionRun))!;

        Assert.NotNull(connection.FindProperty(nameof(AdapterConnection.PollingIntervalSeconds)));
        Assert.NotNull(connection.FindProperty(nameof(AdapterConnection.PollingScheduleMaxAttempts)));
        Assert.NotNull(connection.FindProperty(nameof(AdapterConnection.PollingScheduleConfiguredAtUtc)));
        Assert.Contains(run.GetIndexes(), index =>
            index.IsUnique &&
            index.GetFilter() == "\"State\" = 1" &&
            index.Properties.Select(property => property.Name).SequenceEqual(["ScopeId", "ConnectionId"]));
    }

    [Fact]
    public void Ingress_credentials_are_scope_and_connection_owned_without_token_material()
    {
        using IngestionDbContext dbContext = CreateDbContext();
        IEntityType credential = dbContext.Model.FindEntityType(typeof(AdapterIngressCredential))!;

        Assert.True(credential.FindProperty(nameof(AdapterIngressCredential.Version))!.IsConcurrencyToken);
        Assert.Equal(AdapterIngressCredential.SecretHashLength,
            credential.FindProperty(nameof(AdapterIngressCredential.SecretHash))!.GetMaxLength());
        Assert.Contains(credential.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "ConnectionId"]) &&
            foreignKey.PrincipalEntityType.ClrType == typeof(AdapterConnection));
        Assert.Contains(credential.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "ConnectionId", "State", "ExpiresAtUtc"]));
        Assert.Contains(credential.GetIndexes(), index =>
            index.IsUnique && index.GetFilter() == "\"State\" = 1" &&
            index.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "ConnectionId", "Slot"]));
    }

    [Fact]
    public void Property_projection_and_rebuild_checkpoints_are_scope_owned()
    {
        using IngestionDbContext dbContext = CreateDbContext();
        IEntityType property = dbContext.Model.FindEntityType(typeof(IngestionPropertyProjection))!;
        IEntityType checkpoint = dbContext.Model.FindEntityType(typeof(IngestionProjectionRebuildCheckpoint))!;

        Assert.Equal(["Id"], property.FindPrimaryKey()!.Properties.Select(item => item.Name));
        Assert.Contains(property.GetKeys(), key => key.Properties.Select(item => item.Name)
            .SequenceEqual(["ScopeId", "Id"]));
        Assert.Equal(
            ["ScopeId", "ProjectionName", "RunId"],
            checkpoint.FindPrimaryKey()!.Properties.Select(item => item.Name));
    }

    [Fact]
    public void Reservation_links_and_dispatches_have_scoped_deduplication_barriers()
    {
        using IngestionDbContext dbContext = CreateDbContext();
        IEntityType link = dbContext.Model.FindEntityType(typeof(ReservationSourceLink))!;
        IEntityType dispatch = dbContext.Model.FindEntityType(typeof(ReservationDispatch))!;

        Assert.Contains(link.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "ConnectionId", "SourceSystem", "SourceReference"]));
        Assert.Contains(dispatch.GetIndexes(), index =>
            index.IsUnique && index.Properties.Select(property => property.Name)
                .SequenceEqual(["ScopeId", "TriggerKind", "TriggerId"]));
        Assert.Contains(dispatch.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(["ScopeId", "SourceLinkId"]));
        Assert.Contains(dispatch.GetForeignKeys(), foreignKey =>
            foreignKey.Properties.Select(property => property.Name).SequenceEqual(["ScopeId", "ReceiptId"]));
    }

    [Fact]
    public async Task Proposal_reader_is_property_scoped_and_projects_contract_status()
    {
        await using IngestionDbContext dbContext = CreateDbContext();
        Guid propertyId = Guid.NewGuid();
        ChangeProposal proposal = ChangeProposal.Create(
            Guid.NewGuid(), "tenant-a", propertyId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 2, "test", "{\"change\":true}", DateTimeOffset.UtcNow).Value;
        dbContext.ChangeProposals.Add(proposal);
        await dbContext.SaveChangesAsync();
        ChangeProposalReader reader = new(dbContext);

        ChangeProposalDto? found = await reader.GetAsync(propertyId, proposal.Id, CancellationToken.None);
        ChangeProposalListResponse list = await reader.ListAsync(
            propertyId,
            ChangeProposalStatus.Pending,
            new PageRequest(1, 10),
            CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(ChangeProposalStatus.Pending, found.Status);
        Assert.Equal("test", found.ReasonCode);
        Assert.Equal(SensitiveHistoryStatus.Available, found.SensitiveHistoryStatus);
        Assert.NotNull(found.Diff);
        Assert.Single(list.Proposals);
        Assert.Equal("test", Assert.Single(list.Proposals).ReasonCode);
        Assert.Null(await reader.GetAsync(Guid.NewGuid(), proposal.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Operations_reader_is_property_scoped_and_projects_connection_status()
    {
        await using IngestionDbContext dbContext = CreateDbContext();
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            BunkFy.Adapter.Abstractions.AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, DateTimeOffset.UtcNow).Value;
        dbContext.AdapterConnections.Add(connection);
        await dbContext.SaveChangesAsync();
        IngestionOperationsReader reader = new(dbContext);

        AdapterConnectionDto? found = await reader.GetConnectionAsync(
            propertyId, connection.Id, CancellationToken.None);
        AdapterConnectionListResponse list = await reader.ListConnectionsAsync(
            propertyId, AdapterConnectionStatus.Enabled, new PageRequest(1, 10), CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(AdapterConnectionStatus.Enabled, found.Status);
        Assert.Single(list.Connections);
        Assert.Null(await reader.GetConnectionAsync(Guid.NewGuid(), connection.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Connection_health_projects_latest_run_and_receipt_retention_facts()
    {
        await using IngestionDbContext dbContext = CreateDbContext();
        DateTimeOffset now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            BunkFy.Adapter.Abstractions.AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, now.AddHours(-4)).Value;
        Assert.True(connection.ConfigurePollingSchedule(300, 3, 1, now.AddHours(-2)).IsSuccess);
        IngestionRun succeeded = IngestionRun.Start(
            Guid.NewGuid(), "tenant-a", connection.Id, propertyId, Guid.NewGuid(), 1, null, now.AddHours(-3)).Value;
        Assert.True(succeeded.Complete(
            BunkFy.Adapter.Abstractions.AdapterRunOutcome.Succeeded,
            1, 1, 0, "cursor-1", null, succeeded.Version, now.AddHours(-2)).IsSuccess);
        IngestionRun failed = IngestionRun.Start(
            Guid.NewGuid(), "tenant-a", connection.Id, propertyId, Guid.NewGuid(), 1, "cursor-1", now.AddHours(-1)).Value;
        Assert.True(failed.Complete(
            BunkFy.Adapter.Abstractions.AdapterRunOutcome.Failed,
            0, 0, 0, null, "provider unavailable", failed.Version, now.AddMinutes(-30)).IsSuccess);
        ObservationReceipt pending = CreateReceipt(connection, now.AddHours(-2), now.AddHours(1));
        ObservationReceipt expired = CreateReceipt(connection, now.AddHours(-3), now.AddHours(-1));
        Assert.True(expired.Reject("invalid", now.AddHours(-2)).IsSuccess);
        ObservationReceipt purging = CreateReceipt(connection, now.AddHours(-4), now.AddHours(-2));
        Assert.True(purging.Reject("invalid", now.AddHours(-3)).IsSuccess);
        Assert.True(purging.BeginRawPayloadPurge(
            Guid.NewGuid(), now.AddMinutes(-10), now.AddMinutes(-25)).IsSuccess);
        dbContext.AddRange(connection, succeeded, failed, pending, expired, purging);
        await dbContext.SaveChangesAsync();
        IngestionOperationsReader reader = new(dbContext);

        AdapterConnectionHealthDto? health = await reader.GetConnectionHealthAsync(
            propertyId, connection.Id, now, CancellationToken.None);

        Assert.NotNull(health);
        Assert.Equal(AdapterConnectionOperationalState.LastRunFailed, health.OperationalState);
        Assert.Equal(failed.Id, health.LatestRunId);
        Assert.Equal(IngestionRunStatus.Failed, health.LatestRunStatus);
        Assert.Equal("provider unavailable", health.LatestRunError);
        Assert.Equal(now.AddHours(-2), health.LastSuccessfulRunAtUtc);
        Assert.Equal(1, health.PendingReceiptCount);
        Assert.Equal(2, health.RejectedReceiptCount);
        Assert.Equal(1, health.ExpiredRawPayloadCount);
        Assert.Equal(0, health.ProtectedRawPayloadCount);
        Assert.Equal(1, health.PurgingRawPayloadCount);
        Assert.Equal(0, health.DueSensitiveHistoryCount);
        Assert.Equal(0, health.RedactedSensitiveHistoryCount);
        Assert.Equal(300, health.PollingIntervalSeconds);
        Assert.Equal(3, health.PollingScheduleMaxAttempts);
        Assert.Equal(now.AddHours(-2), health.PollingScheduleConfiguredAtUtc);
        Assert.Equal(now.AddMinutes(-55), health.NextRunExpectedAtUtc);
        Assert.True(health.RunExpected);
        Assert.Null(await reader.GetConnectionHealthAsync(
            Guid.NewGuid(), connection.Id, now, CancellationToken.None));
    }

    [Fact]
    public async Task Polling_schedule_reader_discovers_only_enabled_schedules_across_scopes()
    {
        string databaseName = $"ingestion-schedules-{Guid.NewGuid():N}";
        AdapterConnection tenantA = CreateScheduledConnection("tenant-a", enabled: true);
        AdapterConnection paused = CreateScheduledConnection("tenant-a", enabled: false);
        AdapterConnection tenantB = CreateScheduledConnection("tenant-b", enabled: true);

        await using (IngestionDbContext context = CreateDbContext(databaseName, "tenant-a"))
        {
            context.AddRange(tenantA, paused);
            await context.SaveChangesAsync();
        }

        await using (IngestionDbContext context = CreateDbContext(databaseName, "tenant-b"))
        {
            context.Add(tenantB);
            await context.SaveChangesAsync();
        }

        await using IngestionDbContext readContext = CreateDbContext(databaseName, "tenant-a");
        IReadOnlyList<BunkFy.Modules.Ingestion.Application.Ports.AdapterPollingScheduleDefinition> schedules =
            await new AdapterPollingScheduleReader(readContext).ListActiveAsync(CancellationToken.None);

        Assert.Equal(2, schedules.Count);
        Assert.Contains(schedules, schedule => schedule.ScopeId == "tenant-a" && schedule.ConnectionId == tenantA.Id);
        Assert.Contains(schedules, schedule => schedule.ScopeId == "tenant-b" && schedule.ConnectionId == tenantB.Id);
        Assert.DoesNotContain(schedules, schedule => schedule.ConnectionId == paused.Id);
    }

    [Fact]
    public async Task Property_projection_ignores_stale_source_versions()
    {
        await using IngestionDbContext dbContext = CreateDbContext();
        IngestionPropertyProjectionRepository repository = new(dbContext);
        Guid propertyId = Guid.NewGuid();

        await repository.ApplyAsync(new("tenant-a", propertyId, "Current", "current", true, 2), CancellationToken.None);
        await repository.ApplyAsync(new("tenant-a", propertyId, "Stale", "stale", false, 1), CancellationToken.None);
        await dbContext.SaveChangesAsync();

        IngestionPropertyProjection property = await dbContext.PropertyProjections.SingleAsync();
        Assert.Equal("Current", property.Name);
        Assert.True(property.IsActive);
        Assert.Equal(2, property.SourceVersion);
    }

    [Fact]
    public async Task Raw_payload_claim_excludes_active_proposals_but_not_terminal_proposals()
    {
        await using IngestionDbContext dbContext = CreateDbContext();
        DateTimeOffset now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", Guid.NewGuid(), "fake.http",
            BunkFy.Adapter.Abstractions.AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, now.AddDays(-40)).Value;
        ObservationReceipt pendingReceipt = CreateReceipt(connection, now.AddDays(-40), now.AddDays(-10));
        ObservationReceipt applyingReceipt = CreateReceipt(connection, now.AddDays(-40), now.AddDays(-10));
        ObservationReceipt terminalReceipt = CreateReceipt(connection, now.AddDays(-40), now.AddDays(-10));
        Assert.True(pendingReceipt.MarkProcessed(now.AddDays(-39)).IsSuccess);
        Assert.True(applyingReceipt.MarkProcessed(now.AddDays(-39)).IsSuccess);
        Assert.True(terminalReceipt.MarkProcessed(now.AddDays(-39)).IsSuccess);

        ChangeProposal pending = CreateProposal(connection, pendingReceipt, now.AddDays(-39));
        ChangeProposal applying = CreateProposal(connection, applyingReceipt, now.AddDays(-39));
        Assert.True(applying.BeginApply("staff:42", Guid.NewGuid(), applying.Version, now.AddDays(-1)).IsSuccess);
        ChangeProposal terminal = CreateProposal(connection, terminalReceipt, now.AddDays(-39));
        Assert.True(terminal.Reject(
            "staff:42", "Source is outdated", terminal.Version, now.AddDays(89), now.AddDays(-1)).IsSuccess);
        IngestionPropertyProjectionRepository properties = new(dbContext);
        await properties.ApplyAsync(new(
            "tenant-a", connection.PropertyId, "Held property", "held-property", true, 1),
            CancellationToken.None);
        dbContext.AddRange(connection, pendingReceipt, applyingReceipt, terminalReceipt, pending, applying, terminal);
        await dbContext.SaveChangesAsync();

        IReadOnlyList<BunkFy.Modules.Ingestion.Application.Ports.RawPayloadPurgeCandidate> claimed =
            await new RawPayloadRetentionRepository(dbContext, properties).ClaimBatchAsync(
                Guid.NewGuid(), now, now.AddMinutes(-15), 10, CancellationToken.None);

        Assert.Equal(terminalReceipt.Id, Assert.Single(claimed).ReceiptId);
        Assert.Equal(RawPayloadRetentionState.Available, pendingReceipt.RawPayloadRetentionState);
        Assert.Equal(RawPayloadRetentionState.Available, applyingReceipt.RawPayloadRetentionState);
        Assert.Equal(RawPayloadRetentionState.Purging, terminalReceipt.RawPayloadRetentionState);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        AdapterConnectionHealthDto? health = await new IngestionOperationsReader(dbContext).GetConnectionHealthAsync(
            connection.PropertyId,
            connection.Id,
            now,
            CancellationToken.None);
        Assert.NotNull(health);
        Assert.Equal(0, health.ExpiredRawPayloadCount);
        Assert.Equal(2, health.ProtectedRawPayloadCount);
    }

    [Fact]
    public async Task Sensitive_history_redaction_is_due_ordered_bounded_and_preserves_active_records()
    {
        await using IngestionDbContext dbContext = CreateDbContext();
        DateTimeOffset now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        Guid propertyId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        ChangeProposal dueProposal = ChangeProposal.Create(
            Guid.NewGuid(), "tenant-a", propertyId, connectionId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, "staff-conflict", "{\"guest\":\"Sensitive Proposal\"}", now.AddDays(-100)).Value;
        Assert.True(dueProposal.Reject(
            "staff:42", "Outdated", dueProposal.Version, now.AddDays(-10), now.AddDays(-100).AddMinutes(1)).IsSuccess);
        ChangeProposal activeProposal = ChangeProposal.Create(
            Guid.NewGuid(), "tenant-a", propertyId, connectionId, Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), 1, "active", "{\"guest\":\"Still Needed\"}", now.AddDays(-100)).Value;
        ReservationDispatch dueDispatch = CreateDispatch(propertyId, connectionId, now.AddDays(-100));
        Assert.True(dueDispatch.Complete(
            ReservationDispatchState.Applied,
            Guid.NewGuid(),
            detailsRevision: 2,
            reservationVersion: 3,
            errorCode: null,
            now.AddDays(-20),
            now.AddDays(-100).AddMinutes(1)).IsSuccess);
        ReservationDispatch futureDispatch = CreateDispatch(propertyId, connectionId, now.AddDays(-1));
        Assert.True(futureDispatch.Complete(
            ReservationDispatchState.Applied,
            Guid.NewGuid(),
            detailsRevision: 2,
            reservationVersion: 3,
            errorCode: null,
            now.AddDays(1),
            now.AddHours(-1)).IsSuccess);
        IngestionPropertyProjectionRepository properties = new(dbContext);
        await properties.ApplyAsync(new(
            "tenant-a", propertyId, "Retention property", "retention-property", true, 1),
            CancellationToken.None);
        dbContext.AddRange(dueProposal, activeProposal, dueDispatch, futureDispatch);
        await dbContext.SaveChangesAsync();
        SensitiveHistoryRetentionRepository repository = new(dbContext, properties);

        SensitiveHistoryRedactionBatchResult first = await repository.RedactBatchAsync(
            now, 1, CancellationToken.None);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        SensitiveHistoryRedactionBatchResult second = await repository.RedactBatchAsync(
            now, 10, CancellationToken.None);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        dueDispatch = await dbContext.ReservationDispatches.SingleAsync(item => item.Id == dueDispatch.Id);
        futureDispatch = await dbContext.ReservationDispatches.SingleAsync(item => item.Id == futureDispatch.Id);
        dueProposal = await dbContext.ChangeProposals.SingleAsync(item => item.Id == dueProposal.Id);
        activeProposal = await dbContext.ChangeProposals.SingleAsync(item => item.Id == activeProposal.Id);

        Assert.Equal(new SensitiveHistoryRedactionBatchResult(0, 1), first);
        Assert.Equal(new SensitiveHistoryRedactionBatchResult(1, 0), second);
        Assert.Null(dueDispatch.NormalizedSnapshot);
        Assert.Null(dueProposal.Diff);
        Assert.NotNull(activeProposal.Diff);
        Assert.NotNull(futureDispatch.NormalizedSnapshot);
        Assert.Equal("staff-conflict", dueProposal.ReasonCode);
        Assert.Equal("Outdated", dueProposal.DecisionReason);
    }

    [Fact]
    public async Task Overlapping_legal_holds_block_retention_and_health_counts_are_exclusive()
    {
        await using IngestionDbContext dbContext = CreateDbContext();
        DateTimeOffset now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        Guid propertyId = Guid.NewGuid();
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), "tenant-a", propertyId, "fake.http",
            BunkFy.Adapter.Abstractions.AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, now.AddDays(-100)).Value;
        ObservationReceipt receipt = CreateReceipt(connection, now.AddDays(-100), now.AddDays(-10));
        Assert.True(receipt.MarkProcessed(now.AddDays(-99)).IsSuccess);
        ChangeProposal proposal = CreateProposal(connection, receipt, now.AddDays(-99));
        Assert.True(proposal.Reject(
            "staff:42", "Outdated", proposal.Version, now.AddDays(-10), now.AddDays(-98)).IsSuccess);
        ReservationDispatch dispatch = CreateDispatch(propertyId, connection.Id, now.AddDays(-100));
        Assert.True(dispatch.Complete(
            ReservationDispatchState.Applied,
            Guid.NewGuid(),
            detailsRevision: 2,
            reservationVersion: 3,
            errorCode: null,
            now.AddDays(-10),
            now.AddDays(-99)).IsSuccess);
        LegalHold firstHold = LegalHold.Place(
            Guid.NewGuid(), "tenant-a", propertyId, "Matter A", "user:legal", now.AddDays(-5)).Value;
        LegalHold secondHold = LegalHold.Place(
            Guid.NewGuid(), "tenant-a", propertyId, "Matter B", "user:legal", now.AddDays(-4)).Value;
        IngestionPropertyProjectionRepository properties = new(dbContext);
        await properties.ApplyAsync(new(
            "tenant-a", propertyId, "Held property", "held-property", true, 1),
            CancellationToken.None);
        dbContext.AddRange(connection, receipt, proposal, dispatch, firstHold, secondHold);
        await dbContext.SaveChangesAsync();
        RawPayloadRetentionRepository rawRetention = new(dbContext, properties);
        SensitiveHistoryRetentionRepository historyRetention = new(dbContext, properties);

        IReadOnlyList<RawPayloadPurgeCandidate> heldRaw = await rawRetention.ClaimBatchAsync(
            Guid.NewGuid(), now, now.AddMinutes(-15), 10, CancellationToken.None);
        SensitiveHistoryRedactionBatchResult heldHistory = await historyRetention.RedactBatchAsync(
            now, 10, CancellationToken.None);
        AdapterConnectionHealthDto health = (await new IngestionOperationsReader(dbContext)
            .GetConnectionHealthAsync(propertyId, connection.Id, now, CancellationToken.None))!;

        Assert.Empty(heldRaw);
        Assert.Equal(new SensitiveHistoryRedactionBatchResult(0, 0), heldHistory);
        Assert.Equal(0, health.ExpiredRawPayloadCount);
        Assert.Equal(0, health.ProtectedRawPayloadCount);
        Assert.Equal(1, health.HeldExpiredRawPayloadCount);
        Assert.Equal(0, health.DueSensitiveHistoryCount);
        Assert.Equal(2, health.HeldDueSensitiveHistoryCount);
        Assert.Equal(2, health.ActiveLegalHoldCount);

        Assert.True(firstHold.Release(1, "user:legal", "Matter A closed", now.AddDays(-2)).IsSuccess);
        await dbContext.SaveChangesAsync();
        Assert.Empty(await rawRetention.ClaimBatchAsync(
            Guid.NewGuid(), now, now.AddMinutes(-15), 10, CancellationToken.None));

        Assert.True(secondHold.Release(1, "user:legal", "Matter B closed", now.AddDays(-1)).IsSuccess);
        await dbContext.SaveChangesAsync();
        IReadOnlyList<RawPayloadPurgeCandidate> releasedRaw = await rawRetention.ClaimBatchAsync(
            Guid.NewGuid(), now, now.AddMinutes(-15), 10, CancellationToken.None);
        SensitiveHistoryRedactionBatchResult releasedHistory = await historyRetention.RedactBatchAsync(
            now, 10, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        Assert.Equal(receipt.Id, Assert.Single(releasedRaw).ReceiptId);
        Assert.Equal(new SensitiveHistoryRedactionBatchResult(1, 1), releasedHistory);
        Assert.Equal(2, (await dbContext.PropertyProjections.SingleAsync()).RetentionFenceVersion);
    }

    private static IngestionDbContext CreateDbContext()
    {
        DbContextOptions<IngestionDbContext> options = new DbContextOptionsBuilder<IngestionDbContext>()
            .UseInMemoryDatabase($"ingestion-model-{Guid.NewGuid():N}")
            .Options;
        return new IngestionDbContext(options, new TestScopeContext());
    }

    private static IngestionDbContext CreateDbContext(string databaseName, string scopeId)
    {
        DbContextOptions<IngestionDbContext> options = new DbContextOptionsBuilder<IngestionDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new IngestionDbContext(options, new TestScopeContext(scopeId));
    }

    private static AdapterConnection CreateScheduledConnection(string scopeId, bool enabled)
    {
        AdapterConnection connection = AdapterConnection.Create(
            Guid.NewGuid(), scopeId, Guid.NewGuid(), "fake.http",
            BunkFy.Adapter.Abstractions.AdapterExecutionMode.Polling,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main", null, DateTimeOffset.UtcNow).Value;
        Assert.True(connection.ConfigurePollingSchedule(300, 3, 1, DateTimeOffset.UtcNow).IsSuccess);
        if (!enabled)
        {
            Assert.True(connection.Disable(2, DateTimeOffset.UtcNow).IsSuccess);
        }

        return connection;
    }

    private static ObservationReceipt CreateReceipt(
        AdapterConnection connection,
        DateTimeOffset receivedAtUtc,
        DateTimeOffset retainUntilUtc)
    {
        Guid id = Guid.NewGuid();
        return ObservationReceipt.Create(
            id,
            "tenant-a",
            connection.PropertyId,
            connection.Id,
            runId: null,
            Guid.NewGuid(),
            "reservation.v1",
            $"booking-{id:N}",
            id.ToString("N"),
            $"reservation.v1|booking-{id:N}|{id:N}",
            new string('a', ObservationReceipt.ContentHashLength),
            id,
            retainUntilUtc,
            receivedAtUtc,
            receivedAtUtc,
            receivedAtUtc).Value;
    }

    private static ChangeProposal CreateProposal(
        AdapterConnection connection,
        ObservationReceipt receipt,
        DateTimeOffset createdAtUtc) => ChangeProposal.Create(
            Guid.NewGuid(), receipt.ScopeId, connection.PropertyId, connection.Id, receipt.Id,
            Guid.NewGuid(), receipt.RawPayloadFileId, 1, "test", "{\"change\":true}", createdAtUtc).Value;

    private static ReservationDispatch CreateDispatch(
        Guid propertyId,
        Guid connectionId,
        DateTimeOffset createdAtUtc) => ReservationDispatch.Create(
            Guid.NewGuid(), "tenant-a", Guid.NewGuid(), ReservationDispatchTriggerKind.Observation,
            Guid.NewGuid(), Guid.NewGuid(), connectionId, propertyId, reservationId: null,
            ReservationDispatchKind.Create, sourceRevision: "1", sourceSequence: 1,
            "{\"guest\":\"Sensitive Dispatch\"}", expectedDetailsRevision: null, createdAtUtc).Value;

    private sealed class TestScopeContext(string scopeId = "tenant-a") : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => scopeId;
    }
}
