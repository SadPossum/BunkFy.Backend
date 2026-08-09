namespace BunkFy.Modules.Ingestion.Tests.Persistence;

using System.Text;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using BunkFy.Modules.Ingestion.Persistence.TenantTermination;
using BunkFy.Modules.Workspaces.Contracts;
using Gma.Framework.Messaging.Infrastructure;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionTenantTerminationContributorTests
{
    private const string TenantId =
        "10000000-0000-0000-0000-000000000001";
    private const string Digest =
        "0123456789abcdef0123456789abcdef" +
        "0123456789abcdef0123456789abcdef";
    private const string SecretReference =
        "vault://ingestion/adapter-secret";
    private static readonly Guid ProcessId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CaseId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TerminationEpoch =
        Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid PropertyId =
        Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now =
        new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FrozenAtUtc =
        Now.AddMinutes(-1);

    [Fact]
    public async Task Export_is_deterministic_chunked_and_secret_free()
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        SeedGraph graph = CreateSeedGraph();
        context.AddRange(
            graph.Connection,
            graph.ConnectionOperation,
            graph.Credential,
            graph.Receipt,
            graph.Proposal);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        fences.Current = FrozenFence();
        IngestionTenantTerminationContributor contributor = new(
            context,
            new TestScopeContext(),
            new TestClock(),
            fences);
        CollectingSink first = new();

        TenantTerminationContributionResult result =
            await contributor.ExportAsync(
                Request(),
                first,
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.Completed,
            result.Status);
        Assert.Equal("ingestion.termination.exported", result.ResultCode);
        Assert.Equal(first.Records.Count, result.AffectedCount);
        Assert.Equal(1, result.SelectedProofRevision);
        Assert.Equal(1, result.ResultingProofRevision);
        Assert.Contains(
            first.Records,
            record => record.RecordType ==
                IngestionTenantTerminationMetadata
                    .AdapterConnectionRecordType);
        Assert.Contains(
            first.Records,
            record => record.RecordType ==
                IngestionTenantTerminationMetadata
                    .AdapterCredentialRecordType);
        DataRightsExportRecord connectionOperation = Assert.Single(
            first.Records,
            record => record.RecordType ==
                IngestionTenantTerminationMetadata
                    .ConnectionManagementOperationRecordType);
        Assert.Equal(1, connectionOperation.RecordVersion);
        Assert.DoesNotContain(
            "requestFingerprint",
            FieldsJson(connectionOperation),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "\"kind\":2",
            FieldsJson(connectionOperation),
            StringComparison.Ordinal);
        Assert.Contains(
            first.Records,
            record => record.RecordType ==
                IngestionTenantTerminationMetadata
                    .ObservationReceiptRecordType);
        Assert.Contains(
            first.Records,
            record => record.RecordType ==
                IngestionTenantTerminationMetadata
                    .ChangeProposalRecordType);

        DataRightsExportRecord connection = Assert.Single(
            first.Records,
            record => record.RecordType ==
                IngestionTenantTerminationMetadata
                    .AdapterConnectionRecordType);
        DataRightsExportRecord credential = Assert.Single(
            first.Records,
            record => record.RecordType ==
                IngestionTenantTerminationMetadata
                    .AdapterCredentialRecordType);
        string connectionJson = FieldsJson(connection);
        string credentialJson = FieldsJson(credential);
        Assert.DoesNotContain(
            SecretReference,
            connectionJson,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            AdapterIngressCredential.Sha256HashAlgorithm,
            credentialJson,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            Convert.ToBase64String(graph.SecretHash),
            credentialJson,
            StringComparison.Ordinal);

        DataRightsExportRecord[] chunks = first.Records
            .Where(record => record.RecordType ==
                IngestionTenantTerminationMetadata.LargeTextChunkRecordType)
            .OrderBy(record => Field(
                    record,
                    "ingestion.tenant.large-text-metadata")
                .GetProperty("chunkIndex")
                .GetInt32())
            .ToArray();
        Assert.Equal(3, chunks.Length);
        byte[] reconstructed = chunks
            .SelectMany(record => Field(
                    record,
                    "ingestion.tenant.large-text-content")
                .GetBytesFromBase64())
            .ToArray();
        Assert.Equal(
            graph.LargeDiff,
            Encoding.UTF8.GetString(reconstructed));

        CollectingSink replay = new();
        TenantTerminationContributionResult replayResult =
            await contributor.ExportAsync(
                Request(),
                replay,
                CancellationToken.None);

        Assert.Equal(result.AffectedCount, replayResult.AffectedCount);
        Assert.Equal(
            first.Records.Select(Snapshot).ToArray(),
            replay.Records.Select(Snapshot).ToArray());
        Assert.Equal(
            IngestionTenantTerminationMetadata.ExportFieldIds
                .OrderBy(fieldId => fieldId, StringComparer.Ordinal),
            contributor.ExportDescriptor.FieldIds);
        Assert.Equal(
            [
                TenantTerminationContributionPhase.Export,
                TenantTerminationContributionPhase.Destroy
            ],
            contributor.Descriptor.PhasePlans.Select(plan => plan.Phase));
        Assert.All(
            contributor.Descriptor.PhasePlans,
            plan => Assert.Equal(
                IngestionTenantTerminationMetadata.DependencyOwnerKey,
                Assert.Single(plan.DependsOnOwnerKeys)));
    }

    [Fact]
    public async Task Export_retries_without_records_for_a_different_fence()
    {
        MutableFenceReader fences = new()
        {
            Current = FrozenFence() with { Version = 4 }
        };
        await using IngestionDbContext context = CreateContext(fences);
        IngestionTenantTerminationContributor contributor = new(
            context,
            new TestScopeContext(),
            new TestClock(),
            fences);
        CollectingSink sink = new();

        TenantTerminationContributionResult result =
            await contributor.ExportAsync(
                Request(),
                sink,
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal(
            "ingestion.termination.export-fence-unavailable",
            result.ResultCode);
        Assert.Empty(sink.Records);
    }

    [Fact]
    public async Task Export_retries_without_records_while_payload_purge_is_active()
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        SeedGraph graph = CreateSeedGraph();
        Assert.True(graph.Receipt.MarkProcessed(Now.AddMinutes(1)).IsSuccess);
        Assert.True(graph.Receipt.BeginRawPayloadPurge(
            Guid.NewGuid(),
            Now.AddDays(31),
            Now.AddDays(30)).IsSuccess);
        context.AddRange(graph.Connection, graph.Receipt);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        fences.Current = FrozenFence();
        IngestionTenantTerminationContributor contributor = new(
            context,
            new TestScopeContext(),
            new TestClock(),
            fences);
        CollectingSink sink = new();

        TenantTerminationContributionResult result =
            await contributor.ExportAsync(
                Request(),
                sink,
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            result.Status);
        Assert.Equal(
            "ingestion.termination.export-raw-payload-purge-active",
            result.ResultCode);
        Assert.Empty(sink.Records);
    }

    [Fact]
    public async Task Operational_save_rejects_a_frozen_workspace()
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        context.AdapterConnections.Add(CreateConnection(Guid.NewGuid()));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        fences.Current = FrozenFence();
        context.AdapterConnections.Add(CreateConnection(Guid.NewGuid()));

        IngestionOperationalAdmissionException failure =
            await Assert.ThrowsAsync<
                IngestionOperationalAdmissionException>(
                () => context.SaveChangesAsync());

        Assert.Equal(
            IngestionOperationalAdmissionFailure.Restricted,
            failure.Failure);
        context.ChangeTracker.Clear();
        Assert.Equal(
            1,
            await context.TenantRevisions
                .Select(revision => revision.Revision)
                .SingleAsync());
        Assert.Single(await context.AdapterConnections.ToListAsync());
    }

    [Fact]
    public async Task Operational_saves_advance_the_tenant_revision_once_each()
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        context.AdapterConnections.Add(CreateConnection(Guid.NewGuid()));
        await context.SaveChangesAsync();

        context.AdapterConnections.Add(CreateConnection(Guid.NewGuid()));
        await context.SaveChangesAsync();

        Assert.Equal(
            2,
            await context.TenantRevisions
                .Select(revision => revision.Revision)
                .SingleAsync());
    }

    [Fact]
    public async Task Destroy_blocks_an_active_hold_before_opening_local_progress()
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        context.LegalHolds.Add(LegalHold.Place(
            Guid.NewGuid(),
            TenantId,
            PropertyId,
            "Regulatory preservation",
            "user:privacy-controller",
            Now).Value);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        fences.Current = FrozenFence();
        FakeRawPayloadStore rawPayloads = new();
        IngestionTenantTerminationContributor contributor = new(
            context,
            new TestScopeContext(),
            new TestClock(),
            fences,
            rawPayloads);

        TenantTerminationContributionResult result =
            await contributor.ExecuteAsync(
                DestroyRequest(),
                CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.Blocked,
            result.Status);
        Assert.Equal(
            "ingestion.termination.destroy-active-hold",
            result.ResultCode);
        Assert.Equal(1, result.RemainingActiveCount);
        Assert.Empty(await context.TenantDestroyOperations.ToListAsync());
        Assert.Empty(await context.TenantDestroyReceipts.ToListAsync());
        IngestionTenantRevision state =
            await context.TenantRevisions.SingleAsync();
        Assert.True(state.IsOpen);
        Assert.Null(state.DestroyOperationId);
        Assert.Empty(rawPayloads.DeletedCoordinates);
    }

    [Fact]
    public async Task Destroy_resumes_storage_and_database_work_then_replays()
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        SeedGraph graph = CreateSeedGraph();
        context.AddRange(
            graph.Connection,
            graph.ConnectionOperation,
            graph.Credential,
            graph.Receipt,
            graph.Proposal);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        fences.Current = FrozenFence();
        FakeRawPayloadStore rawPayloads = new();
        rawPayloads.Seed(
            graph.Receipt.RawPayloadFileId,
            TenantId,
            graph.Receipt.ConnectionId);
        IngestionTenantTerminationContributor contributor = new(
            context,
            new TestScopeContext(),
            new TestClock(),
            fences,
            rawPayloads);
        TenantTerminationContributionRequest request = DestroyRequest();

        TenantTerminationContributionResult result =
            await ExecuteUntilCompleteAsync(contributor, request);

        Assert.Equal(
            TenantTerminationContributionStatus.Completed,
            result.Status);
        Assert.Equal("ingestion.termination.destroyed", result.ResultCode);
        Assert.True(result.AffectedCount >= 6);
        Assert.Equal(1, result.SelectedProofRevision);
        Assert.Equal(2, result.ResultingProofRevision);
        Assert.Empty(await context.TenantDestroyOperations.ToListAsync());
        IngestionTenantDestroyReceipt receipt =
            await context.TenantDestroyReceipts.SingleAsync();
        Assert.Equal(1, receipt.RemovedRawPayloadCount);
        Assert.Equal(result.AffectedCount, receipt.RemovedArtifactCount);
        Assert.Equal(
            IngestionTenantLifecycleStatus.Closed,
            (await context.TenantRevisions.SingleAsync()).LifecycleStatus);
        Assert.False(await HasOwnerRecordsAsync(context));
        Assert.Single(rawPayloads.DeletedCoordinates);

        TenantTerminationContributionResult replay =
            await contributor.ExecuteAsync(request, CancellationToken.None);
        Assert.Equal(result, replay);

        TenantTerminationContributionResult conflict =
            await contributor.ExecuteAsync(
                request with { ExecutingActorId = "other:executor" },
                CancellationToken.None);
        Assert.Equal(
            TenantTerminationContributionStatus.Failed,
            conflict.Status);
        Assert.Equal(
            "ingestion.termination.destroy-conflict",
            conflict.ResultCode);

        context.TenantDestroyReceipts.Remove(receipt);
        InvalidOperationException receiptMutation =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.SaveChangesAsync());
        Assert.Contains("append-only", receiptMutation.Message);
        context.ChangeTracker.Clear();
        fences.Current = null;
        context.AdapterConnections.Add(CreateConnection(Guid.NewGuid()));
        IngestionOperationalAdmissionException closedFailure =
            await Assert.ThrowsAsync<IngestionOperationalAdmissionException>(
                () => context.SaveChangesAsync());
        Assert.Equal(
            IngestionOperationalAdmissionFailure.Restricted,
            closedFailure.Failure);

        context.ChangeTracker.Clear();
        context.OutboxMessages.Add(new OutboxMessage(
            Guid.NewGuid(),
            "bunkfy.ingestion.lifecycle-test.v1",
            "lifecycle-test",
            version: 1,
            TenantId,
            Now,
            "{}",
            Now));
        IngestionOperationalAdmissionException messageFailure =
            await Assert.ThrowsAsync<IngestionOperationalAdmissionException>(
                () => context.SaveChangesAsync());
        Assert.Equal(
            IngestionOperationalAdmissionFailure.Restricted,
            messageFailure.Failure);
    }

    [Fact]
    public async Task Destroy_does_not_record_raw_progress_until_absence_is_proven()
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        SeedGraph graph = CreateSeedGraph();
        context.AddRange(graph.Connection, graph.Receipt);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        fences.Current = FrozenFence();
        FakeRawPayloadStore rawPayloads = new()
        {
            PreserveOnDelete = true
        };
        rawPayloads.Seed(
            graph.Receipt.RawPayloadFileId,
            TenantId,
            graph.Receipt.ConnectionId);
        IngestionTenantTerminationContributor contributor = new(
            context,
            new TestScopeContext(),
            new TestClock(),
            fences,
            rawPayloads);
        TenantTerminationContributionRequest request = DestroyRequest();

        TenantTerminationContributionResult retry =
            await contributor.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(
            TenantTerminationContributionStatus.RetryRequired,
            retry.Status);
        Assert.Equal(
            "ingestion.termination.destroy-raw-payload-retry",
            retry.ResultCode);
        IngestionTenantDestroyOperation operation =
            await context.TenantDestroyOperations.SingleAsync();
        Assert.Equal(
            IngestionTenantDestroyStage.RawPayloadObjects,
            operation.Stage);
        Assert.Equal(0, operation.RemovedRawPayloadCount);
        Assert.Single(await context.ObservationReceipts.ToListAsync());

        rawPayloads.PreserveOnDelete = false;
        TenantTerminationContributionResult result =
            await ExecuteUntilCompleteAsync(contributor, request);
        Assert.Equal(
            TenantTerminationContributionStatus.Completed,
            result.Status);
    }

    [Fact]
    public void Destroy_progress_rejects_batches_above_the_persisted_bound()
    {
        IngestionTenantDestroyOperation operation = Assert.IsType<
            IngestionTenantDestroyOperation>(
            IngestionTenantDestroyOperation.TryCreate(
                Guid.NewGuid(),
                TenantId,
                Digest,
                selectedRevision: 4,
                IngestionTenantDestroyOperation.MaximumBatchSize,
                Now));

        Assert.False(operation.RecordRawPayloadBatch(
            IngestionTenantDestroyOperation.MaximumBatchSize + 1,
            Digest,
            stageCompleted: false,
            Now.AddMinutes(1)));
        Assert.Equal(0, operation.RemovedRawPayloadCount);
        Assert.True(operation.AdvanceEmptyStage(Now.AddMinutes(1)));
        Assert.False(operation.RecordBatch(
            IngestionTenantDestroyStage.OutboxMessages,
            IngestionTenantDestroyOperation.MaximumBatchSize + 1,
            Digest,
            stageCompleted: false,
            Now.AddMinutes(2)));
        Assert.True(operation.RecordBatch(
            IngestionTenantDestroyStage.OutboxMessages,
            IngestionTenantDestroyOperation.MaximumBatchSize,
            Digest,
            stageCompleted: false,
            Now.AddMinutes(2)));
        Assert.Equal(
            IngestionTenantDestroyOperation.MaximumBatchSize,
            operation.RemovedRecordCount);
    }

    [Fact]
    public async Task Coordinated_direct_mutation_requires_a_clean_tracker()
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        context.AdapterConnections.Add(CreateConnection(Guid.NewGuid()));
        bool mutationInvoked = false;

        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                context.ExecuteCoordinatedMutationAsync(
                    _ =>
                    {
                        mutationInvoked = true;
                        return Task.FromResult(0);
                    },
                    CancellationToken.None));

        Assert.Equal(
            "Coordinated Ingestion direct mutations require a clean change tracker.",
            failure.Message);
        Assert.False(mutationInvoked);
        Assert.Equal(
            EntityState.Added,
            context.Entry(context.AdapterConnections.Local.Single()).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Anonymisation_owner_proof_is_append_only(
        bool deleteTombstone)
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        IngestionAnonymisationReceipt receipt =
            CreateAnonymisationReceipt();
        IngestionAnonymisationTombstone tombstone =
            CreateAnonymisationTombstone(receipt);
        context.AddRange(receipt, tombstone);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        if (deleteTombstone)
        {
            IngestionAnonymisationTombstone persisted =
                await context.AnonymisationTombstones.SingleAsync();
            context.Entry(persisted).State = EntityState.Deleted;
        }
        else
        {
            IngestionAnonymisationReceipt persisted =
                await context.AnonymisationReceipts.SingleAsync();
            context.Entry(persisted).State = EntityState.Modified;
        }

        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.SaveChangesAsync());

        Assert.Contains(
            "append-only",
            failure.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Connection_management_operation_receipts_are_append_only()
    {
        MutableFenceReader fences = new();
        await using IngestionDbContext context = CreateContext(fences);
        Guid connectionId = Guid.NewGuid();
        IngestionConnectionManagementOperation operation = new(
            new IngestionConnectionManagementOperationRecord(
                connectionId,
                TenantId,
                PropertyId,
                connectionId,
                IngestionConnectionManagementMutationKind.ConnectionCreate,
                ExpectedVersion: 0,
                Digest,
                ResultVersion: 1,
                Now));
        context.ConnectionManagementOperations.Add(operation);
        await context.SaveChangesAsync();

        context.Entry(operation).Property(
            nameof(IngestionConnectionManagementOperation.ResultVersion))
            .CurrentValue = 2L;
        InvalidOperationException failure =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.SaveChangesAsync());

        Assert.Contains("append-only", failure.Message, StringComparison.Ordinal);
    }

    private static SeedGraph CreateSeedGraph()
    {
        Guid connectionId = Guid.NewGuid();
        AdapterConnection connection = CreateConnection(connectionId);
        IngestionConnectionManagementOperation connectionOperation = new(
            new IngestionConnectionManagementOperationRecord(
                Guid.NewGuid(),
                TenantId,
                PropertyId,
                connectionId,
                IngestionConnectionManagementMutationKind.ConnectionUpdate,
                ExpectedVersion: 1,
                Digest,
                ResultVersion: 1,
                Now));
        byte[] secretHash = Enumerable
            .Repeat((byte)0xa5, AdapterIngressCredential.SecretHashLength)
            .ToArray();
        AdapterIngressCredential credential =
            AdapterIngressCredential.Create(
                Guid.NewGuid(),
                TenantId,
                connectionId,
                "fake.http",
                adapterProtocolVersion: 1,
                configurationSchemaVersion: 1,
                "booking",
                slot: 1,
                "primary",
                AdapterIngressCredential.Sha256HashAlgorithm,
                secretHash,
                Now.AddDays(30),
                "user:owner",
                Now).Value;
        Guid receiptId = Guid.NewGuid();
        Guid payloadFileId = Guid.NewGuid();
        ObservationReceipt receipt = ObservationReceipt.Create(
            receiptId,
            TenantId,
            PropertyId,
            connectionId,
            runId: null,
            Guid.NewGuid(),
            "reservation.changed",
            "booking-123",
            "revision-1",
            "reservation.changed|booking-123|revision-1",
            new string('a', ObservationReceipt.ContentHashLength),
            TestObservationCountryPolicyEvidence.Create(Now),
            payloadFileId,
            Now.AddDays(30),
            Now,
            Now,
            Now).Value;
        string largeDiff = new('x', 24_101);
        ChangeProposal proposal = ChangeProposal.Create(
            Guid.NewGuid(),
            TenantId,
            PropertyId,
            connectionId,
            receiptId,
            Guid.NewGuid(),
            payloadFileId,
            baseReservationDetailsRevision: 1,
            "staff-conflict",
            largeDiff,
            Now).Value;
        return new(
            connection,
            connectionOperation,
            credential,
            receipt,
            proposal,
            secretHash,
            largeDiff);
    }

    private static AdapterConnection CreateConnection(Guid connectionId) =>
        AdapterConnection.Create(
            connectionId,
            TenantId,
            PropertyId,
            "fake.http",
            AdapterExecutionMode.Push,
            IngestionConflictPolicy.SuggestionsOnly,
            "configuration://main",
            SecretReference,
            Now).Value;

    private static IngestionAnonymisationReceipt
        CreateAnonymisationReceipt() =>
        IngestionAnonymisationReceipt.Create(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            PropertyId,
            CaseId,
            approvalRevision: 1,
            operationRevision: 2,
            Guid.NewGuid(),
            selectedSourceLinkVersion: 1,
            resultingSourceLinkVersion: 2,
            graphRecordCount: 1,
            fingerprintCount: 1,
            rawPayloadCount: 0,
            Digest,
            Digest,
            Digest,
            "user:privacy",
            Now).Value;

    private static IngestionAnonymisationTombstone
        CreateAnonymisationTombstone(
            IngestionAnonymisationReceipt receipt)
    {
        IngestionAnonymisationTombstone tombstone =
            IngestionAnonymisationTombstone.BeginExecution(
                TenantId,
                receipt.SourceLinkId,
                receipt.PropertyId,
                Guid.NewGuid(),
                receipt.SelectedSourceLinkVersion,
                receipt.WorkItemId,
                receipt.IdempotencyKey,
                receipt.CaseId,
                receipt.ApprovalRevision,
                receipt.OperationRevision,
                receipt.Id,
                receipt.ApprovalEvidenceSha256,
                receipt.PolicyEvidenceSha256,
                receipt.OperationFenceSha256,
                receipt.ActorId,
                receipt.GraphRecordCount,
                receipt.FingerprintCount,
                receipt.RawPayloadCount,
                Now).Value;
        Assert.True(tombstone.CompleteExecution(receipt).IsSuccess);
        return tombstone;
    }

    private static WorkspaceTerminationFenceSnapshot FrozenFence() =>
        new(
            ProcessId,
            TerminationEpoch,
            WorkspaceTerminationFenceState.Frozen,
            Version: 3);

    private static TenantTerminationExportRequest Request() =>
        new(
            new TenantTerminationContributionRequest(
                TenantTerminationContract.CurrentVersion,
                TenantId,
                ProcessId,
                CaseId,
                ApprovalRevision: 1,
                OperationRevision: 2,
                TerminationEpoch,
                TenantTerminationContributionPhase.Export,
                Guid.Parse("60000000-0000-0000-0000-000000000001"),
                Guid.Parse("70000000-0000-0000-0000-000000000001"),
                Digest,
                "termination-exporter",
                Now.AddMinutes(5)),
            FreezeOperationRevision: 1,
            WorkspaceFenceRevision: 3,
            Digest,
            FrozenAtUtc);

    private static TenantTerminationContributionRequest DestroyRequest() =>
        new(
            TenantTerminationContract.CurrentVersion,
            TenantId,
            ProcessId,
            CaseId,
            ApprovalRevision: 1,
            OperationRevision: 3,
            TerminationEpoch,
            TenantTerminationContributionPhase.Destroy,
            Guid.Parse("80000000-0000-0000-0000-000000000001"),
            Guid.Parse("90000000-0000-0000-0000-000000000001"),
            Digest,
            "termination-destroyer",
            Now.AddHours(1));

    private static async Task<TenantTerminationContributionResult>
        ExecuteUntilCompleteAsync(
            IngestionTenantTerminationContributor contributor,
            TenantTerminationContributionRequest request)
    {
        TenantTerminationContributionResult result =
            await contributor.ExecuteAsync(request, CancellationToken.None);
        int attempts = 1;
        while (result.Status ==
                TenantTerminationContributionStatus.RetryRequired &&
            attempts < 100)
        {
            result = await contributor.ExecuteAsync(
                request,
                CancellationToken.None);
            attempts++;
        }

        return result;
    }

    private static async Task<bool> HasOwnerRecordsAsync(
        IngestionDbContext context) =>
        await context.AdapterConnections.AnyAsync() ||
        await context.ConnectionManagementOperations.AnyAsync() ||
        await context.AdapterIngressCredentials.AnyAsync() ||
        await context.AdapterIngressTenantControls.AnyAsync() ||
        await context.PropertyProjections.AnyAsync() ||
        await context.ProjectionRebuildCheckpoints.AnyAsync() ||
        await context.Runs.AnyAsync() ||
        await context.ObservationReceipts.AnyAsync() ||
        await context.ObservationReprocessingAttempts.AnyAsync() ||
        await context.ObservationReprocessingOutputs.AnyAsync() ||
        await context.ChangeProposals.AnyAsync() ||
        await context.ReservationSourceLinks.AnyAsync() ||
        await context.ReservationDispatches.AnyAsync() ||
        await context.LegalHolds.AnyAsync() ||
        await context.RetentionExecutions.AnyAsync() ||
        await context.AnonymisationTombstones.AnyAsync() ||
        await context.AnonymisationReceipts.AnyAsync() ||
        await context.AnonymisationFingerprints.AnyAsync() ||
        await context.AnonymisationRecordPlan.AnyAsync() ||
        await context.OutboxMessages.AnyAsync() ||
        await context.InboxMessages.AnyAsync();

    private static JsonElement Field(
        DataRightsExportRecord record,
        string fieldId) =>
        Assert.Single(
            record.Fields,
            field => field.FieldId == fieldId).Value;

    private static string FieldsJson(DataRightsExportRecord record) =>
        string.Join(
            '|',
            record.Fields
                .OrderBy(field => field.FieldId, StringComparer.Ordinal)
                .Select(field =>
                    $"{field.FieldId}:{field.Value.GetRawText()}"));

    private static string Snapshot(DataRightsExportRecord record) =>
        $"{record.RecordType}|{record.RecordId:N}|" +
        $"{record.RecordVersion}|{FieldsJson(record)}";

    private static IngestionDbContext CreateContext(
        IWorkspaceTerminationFenceReader fences)
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
        return new IngestionDbContext(
            options,
            new TestScopeContext(),
            fences);
    }

    private sealed record SeedGraph(
        AdapterConnection Connection,
        IngestionConnectionManagementOperation ConnectionOperation,
        AdapterIngressCredential Credential,
        ObservationReceipt Receipt,
        ChangeProposal Proposal,
        byte[] SecretHash,
        string LargeDiff);

    private sealed class CollectingSink : IDataRightsExportSink
    {
        public List<DataRightsExportRecord> Records { get; } = [];

        public ValueTask WriteAsync(
            DataRightsExportRecord record,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class MutableFenceReader
        : IWorkspaceTerminationFenceReader
    {
        public WorkspaceTerminationFenceSnapshot? Current { get; set; }

        public Task<WorkspaceTerminationFenceSnapshot?> GetCurrentAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(this.Current);
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FakeRawPayloadStore : IRawPayloadStore
    {
        private readonly HashSet<(Guid PayloadId, string ScopeId, Guid ConnectionId)>
            payloads = [];

        public bool PreserveOnDelete { get; set; }

        public List<(Guid PayloadId, string ScopeId, Guid ConnectionId)>
            DeletedCoordinates
        { get; } = [];

        public void Seed(
            Guid payloadId,
            string scopeId,
            Guid connectionId) =>
            this.payloads.Add((payloadId, scopeId, connectionId));

        public Task StoreAsync(
            RawPayloadWrite write,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.payloads.Add((
                write.PayloadId,
                write.ScopeId,
                write.ConnectionId));
            return Task.CompletedTask;
        }

        public Task<RawPayloadRead?> ReadAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RawPayloadRead? result = this.payloads.Contains((
                payloadId,
                scopeId,
                connectionId))
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
            this.DeletedCoordinates.Add((
                payloadId,
                scopeId,
                connectionId));
            bool removed = !this.PreserveOnDelete &&
                this.payloads.Remove((payloadId, scopeId, connectionId));
            return Task.FromResult(removed);
        }
    }
}
