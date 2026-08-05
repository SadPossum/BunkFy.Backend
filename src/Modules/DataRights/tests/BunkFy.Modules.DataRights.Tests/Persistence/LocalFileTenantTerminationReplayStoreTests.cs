namespace BunkFy.Modules.DataRights.Tests.Persistence;

using System.Text.Json.Nodes;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Persistence;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class LocalFileTenantTerminationReplayStoreTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 4, 16, 0, 0, TimeSpan.Zero);
    private static readonly string PolicyDigest = new('a', 64);
    private static readonly string CatalogDigest = new('b', 64);

    [Fact]
    public async Task Intent_append_is_idempotent_encrypted_and_conflict_safe()
    {
        await using TestDirectory directory = new();
        LocalFileTenantTerminationReplayStore store =
            CreateStore(directory.Path);
        TenantTerminationReplayIntent intent = CreateIntent();
        TenantTerminationReplayJournalEntry entry =
            TenantTerminationReplayJournalEntry.ForIntent(intent);

        TenantTerminationReplayAppendReceipt first = await store.AppendAsync(
            entry,
            CancellationToken.None);
        TenantTerminationReplayAppendReceipt replay = await store.AppendAsync(
            entry,
            CancellationToken.None);
        TenantTerminationReplayIntent? restored = await store.ReadIntentAsync(
            intent.TenantId,
            intent.ProcessId,
            CancellationToken.None);

        Assert.Equal(first, replay);
        Assert.Equal(intent, restored);

        TenantTerminationReplayIntent conflicting = CreateIntent(
            exportRequested: false);
        TenantTerminationReplayStoreException failure =
            await Assert.ThrowsAsync<TenantTerminationReplayStoreException>(
                () => store.AppendAsync(
                    TenantTerminationReplayJournalEntry.ForIntent(conflicting),
                    CancellationToken.None));
        Assert.Equal(
            LocalFileTenantTerminationReplayStore.ConflictCode,
            failure.Code);

        string persisted = string.Join(
            '\n',
            Directory.GetFiles(
                    directory.Path,
                    "*",
                    SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(
                    ".append.lock",
                    StringComparison.Ordinal))
                .Select(File.ReadAllText));
        Assert.DoesNotContain(intent.TenantId, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(intent.RequestedBy, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(intent.ApprovedBy, persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(
            intent.ExecutingActorId,
            persisted,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Intent_ciphertext_tampering_fails_closed()
    {
        await using TestDirectory directory = new();
        LocalFileTenantTerminationReplayStore store =
            CreateStore(directory.Path);
        TenantTerminationReplayIntent intent = CreateIntent();
        _ = await store.AppendAsync(
            TenantTerminationReplayJournalEntry.ForIntent(intent),
            CancellationToken.None);
        string recordPath = FindRecordPaths(directory.Path).Single();
        JsonObject record = JsonNode.Parse(await File.ReadAllTextAsync(
            recordPath))!.AsObject();
        string ciphertext = record["ciphertextBase64"]!.GetValue<string>();
        record["ciphertextBase64"] =
            (ciphertext[0] == 'A' ? 'B' : 'A') + ciphertext[1..];
        await File.WriteAllTextAsync(recordPath, record.ToJsonString());

        TenantTerminationReplayStoreException failure =
            await Assert.ThrowsAsync<TenantTerminationReplayStoreException>(
                () => store.ReadIntentAsync(
                    intent.TenantId,
                    intent.ProcessId,
                    CancellationToken.None));

        Assert.Equal(
            LocalFileTenantTerminationReplayStore.IntegrityCode,
            failure.Code);
    }

    [Fact]
    public async Task Append_is_idempotent_encrypted_and_bounded()
    {
        await using TestDirectory directory = new();
        LocalFileTenantTerminationReplayStore store =
            CreateStore(directory.Path);
        TenantTerminationReplayDispatch dispatch = CreateDispatch();
        TenantTerminationReplayJournalEntry dispatchEntry =
            TenantTerminationReplayJournalEntry.ForDispatch(dispatch);
        TenantTerminationReplayResult result = CreateResult(dispatch);

        TenantTerminationReplayStoreReadiness readiness =
            await store.CheckReadinessAsync(CancellationToken.None);
        TenantTerminationReplayAppendReceipt first = await store.AppendAsync(
            dispatchEntry,
            CancellationToken.None);
        TenantTerminationReplayAppendReceipt replay = await store.AppendAsync(
            dispatchEntry,
            CancellationToken.None);
        _ = await store.AppendAsync(
            TenantTerminationReplayJournalEntry.ForResult(dispatch, result),
            CancellationToken.None);
        TenantTerminationReplayAttempt? attempt = await store.ReadAttemptAsync(
            dispatch.Coordinate,
            CancellationToken.None);
        TenantTerminationReplayPage firstPage = await store.ReadAfterAsync(
            dispatch.Coordinate.TenantId,
            dispatch.Coordinate.ProcessId,
            TenantTerminationReplayCursor.Genesis,
            pageSize: 1,
            CancellationToken.None);
        TenantTerminationReplayPage secondPage = await store.ReadAfterAsync(
            dispatch.Coordinate.TenantId,
            dispatch.Coordinate.ProcessId,
            firstPage.NextCursor,
            pageSize: 1,
            CancellationToken.None);
        TenantTerminationReplayCheckpoint checkpoint =
            await store.ReadTrustedCheckpointAsync(
                dispatch.Coordinate.TenantId,
                dispatch.Coordinate.ProcessId,
                CancellationToken.None);

        Assert.True(readiness.IsReady);
        Assert.False(readiness.IsProductionGrade);
        Assert.Equal(first, replay);
        Assert.NotNull(attempt);
        Assert.Equal(dispatch, attempt.Dispatch);
        Assert.Equal(result, attempt.Result);
        Assert.Single(firstPage.Entries);
        Assert.True(firstPage.HasMore);
        Assert.Single(secondPage.Entries);
        Assert.False(secondPage.HasMore);
        Assert.Equal(2, checkpoint.Cursor.Sequence);

        string persisted = string.Join(
            '\n',
            Directory.GetFiles(
                    directory.Path,
                    "*",
                    SearchOption.AllDirectories)
                .Where(path => !path.EndsWith(
                    ".append.lock",
                    StringComparison.Ordinal))
                .Select(File.ReadAllText));
        Assert.DoesNotContain("tenant-a", persisted, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "reservations",
            persisted,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "operator:executor",
            persisted,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Conflicting_reuse_of_an_attempt_identity_is_rejected()
    {
        await using TestDirectory directory = new();
        LocalFileTenantTerminationReplayStore store =
            CreateStore(directory.Path);
        TenantTerminationReplayDispatch first = CreateDispatch();
        TenantTerminationReplayDispatch conflicting = CreateDispatch(
            deadlineUtc: Now.AddMinutes(6));
        _ = await store.AppendAsync(
            TenantTerminationReplayJournalEntry.ForDispatch(first),
            CancellationToken.None);

        TenantTerminationReplayStoreException failure =
            await Assert.ThrowsAsync<TenantTerminationReplayStoreException>(
                () => store.AppendAsync(
                    TenantTerminationReplayJournalEntry.ForDispatch(
                        conflicting),
                    CancellationToken.None));

        Assert.Equal(
            LocalFileTenantTerminationReplayStore.ConflictCode,
            failure.Code);
    }

    [Fact]
    public async Task Result_requires_its_exact_durable_dispatch()
    {
        await using TestDirectory directory = new();
        LocalFileTenantTerminationReplayStore store =
            CreateStore(directory.Path);
        TenantTerminationReplayDispatch dispatch = CreateDispatch();

        TenantTerminationReplayStoreException failure =
            await Assert.ThrowsAsync<TenantTerminationReplayStoreException>(
                () => store.AppendAsync(
                    TenantTerminationReplayJournalEntry.ForResult(
                        dispatch,
                        CreateResult(dispatch)),
                    CancellationToken.None));

        Assert.Equal(
            LocalFileTenantTerminationReplayStore.ConflictCode,
            failure.Code);
    }

    [Fact]
    public async Task Ciphertext_tampering_fails_closed()
    {
        await using TestDirectory directory = new();
        LocalFileTenantTerminationReplayStore store =
            CreateStore(directory.Path);
        TenantTerminationReplayDispatch dispatch = CreateDispatch();
        _ = await store.AppendAsync(
            TenantTerminationReplayJournalEntry.ForDispatch(dispatch),
            CancellationToken.None);
        string recordPath = FindRecordPaths(directory.Path).Single();
        JsonObject record = JsonNode.Parse(await File.ReadAllTextAsync(
            recordPath))!.AsObject();
        string ciphertext = record["ciphertextBase64"]!.GetValue<string>();
        record["ciphertextBase64"] =
            (ciphertext[0] == 'A' ? 'B' : 'A') + ciphertext[1..];
        await File.WriteAllTextAsync(recordPath, record.ToJsonString());

        TenantTerminationReplayStoreException failure =
            await Assert.ThrowsAsync<TenantTerminationReplayStoreException>(
                () => store.ReadAttemptAsync(
                    dispatch.Coordinate,
                    CancellationToken.None));

        Assert.Equal(
            LocalFileTenantTerminationReplayStore.IntegrityCode,
            failure.Code);
    }

    [Fact]
    public async Task Deleting_a_committed_tail_record_fails_closed()
    {
        await using TestDirectory directory = new();
        LocalFileTenantTerminationReplayStore store =
            CreateStore(directory.Path);
        TenantTerminationReplayDispatch dispatch = CreateDispatch();
        _ = await store.AppendAsync(
            TenantTerminationReplayJournalEntry.ForDispatch(dispatch),
            CancellationToken.None);
        _ = await store.AppendAsync(
            TenantTerminationReplayJournalEntry.ForResult(
                dispatch,
                CreateResult(dispatch)),
            CancellationToken.None);
        File.Delete(FindRecordPaths(directory.Path)[^1]);

        TenantTerminationReplayStoreException failure =
            await Assert.ThrowsAsync<TenantTerminationReplayStoreException>(
                () => store.ReadTrustedCheckpointAsync(
                    dispatch.Coordinate.TenantId,
                    dispatch.Coordinate.ProcessId,
                    CancellationToken.None));

        Assert.Equal(
            LocalFileTenantTerminationReplayStore.IntegrityCode,
            failure.Code);
    }

    [Fact]
    public async Task Restart_recovers_records_written_after_a_trusted_checkpoint()
    {
        await using TestDirectory directory = new();
        LocalFileTenantTerminationReplayStore store =
            CreateStore(directory.Path);
        TenantTerminationReplayDispatch dispatch = CreateDispatch();
        _ = await store.AppendAsync(
            TenantTerminationReplayJournalEntry.ForDispatch(dispatch),
            CancellationToken.None);
        string checkpointPath = Directory.GetFiles(
            directory.Path,
            "checkpoint.json",
            SearchOption.AllDirectories).Single();
        byte[] priorCheckpoint = await File.ReadAllBytesAsync(checkpointPath);
        TenantTerminationReplayResult result = CreateResult(dispatch);
        _ = await store.AppendAsync(
            TenantTerminationReplayJournalEntry.ForResult(dispatch, result),
            CancellationToken.None);
        await File.WriteAllBytesAsync(checkpointPath, priorCheckpoint);

        LocalFileTenantTerminationReplayStore restarted =
            CreateStore(directory.Path);
        TenantTerminationReplayCheckpoint recovered =
            await restarted.ReadTrustedCheckpointAsync(
                dispatch.Coordinate.TenantId,
                dispatch.Coordinate.ProcessId,
                CancellationToken.None);
        TenantTerminationReplayAttempt? attempt =
            await restarted.ReadAttemptAsync(
                dispatch.Coordinate,
                CancellationToken.None);

        Assert.Equal(2, recovered.Cursor.Sequence);
        Assert.NotNull(attempt);
        Assert.Equal(result, attempt.Result);
    }

    private static LocalFileTenantTerminationReplayStore CreateStore(
        string path)
    {
        DataRightsTenantTerminationReplayOptions replayOptions = new()
        {
            Provider = DataRightsTenantTerminationReplayProvider.LocalFile,
            LocalFilePath = path,
            MaximumPageSize = 2
        };
        DataRightsReplayEnvelopeOptions keyOptions = new()
        {
            ActiveKeyVersion = 1,
            Keys =
            {
                [1] = DataRightsReplayEnvelopeOptions.DevelopmentKeyBase64
            }
        };
        return new(
            Options.Create(replayOptions),
            Options.Create(keyOptions),
            new TestHostEnvironment(path),
            new FixedTimeProvider(Now.AddMinutes(4)));
    }

    private static TenantTerminationReplayDispatch CreateDispatch(
        DateTimeOffset? deadlineUtc = null) =>
        TenantTerminationReplayDispatch.Create(
            Request(deadlineUtc),
            "reservations",
            catalogVersion: 3,
            CatalogDigest,
            TenantTerminationExecutionBoundary.GlobalControlTask,
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            taskAttempt: 1,
            Now.AddMinutes(1));

    private static TenantTerminationReplayIntent CreateIntent(
        bool exportRequested = true) =>
        TenantTerminationReplayIntent.Create(
            "tenant-a",
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Guid.Parse("22222222-3333-4444-5555-666666666666"),
            DataRightsRequesterRelationship.TenantOwner,
            "operator:requester",
            Now.AddMinutes(-3),
            exportRequested,
            approvalRevision: 3,
            "operator:approver",
            Now.AddMinutes(-2),
            PolicyDigest,
            CatalogDigest,
            Guid.Parse("33333333-4444-5555-6666-777777777777"),
            Guid.Parse("44444444-5555-6666-7777-888888888888"),
            "operator:executor",
            Now.AddMinutes(-1),
            Now);

    private static TenantTerminationContributionRequest Request(
        DateTimeOffset? deadlineUtc) =>
        new(
            TenantTerminationContract.CurrentVersion,
            "tenant-a",
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Guid.Parse("22222222-3333-4444-5555-666666666666"),
            ApprovalRevision: 7,
            OperationRevision: 8,
            Guid.Parse("33333333-4444-5555-6666-777777777777"),
            TenantTerminationContributionPhase.Destroy,
            Guid.Parse("44444444-5555-6666-7777-888888888888"),
            Guid.Parse("55555555-6666-7777-8888-999999999999"),
            PolicyDigest,
            "operator:executor",
            deadlineUtc ?? Now.AddMinutes(5));

    private static TenantTerminationReplayResult CreateResult(
        TenantTerminationReplayDispatch dispatch) =>
        TenantTerminationReplayResult.Create(
            dispatch,
            new(
                TenantTerminationContributionStatus.Completed,
                "reservations.termination.destroyed",
                AffectedCount: 12,
                RetainedMinimumCount: 2,
                RemainingActiveCount: 0,
                HoldReviewAtUtc: null,
                SelectedProofRevision: 4,
                ResultingProofRevision: 5,
                CatalogVersion: 3,
                CatalogDigest,
                Now.AddMinutes(2)),
            Now.AddMinutes(3));

    private static string[] FindRecordPaths(string rootPath) =>
        [.. Directory.GetFiles(
                rootPath,
                "*.replay.json",
                SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)];

    private sealed class FixedTimeProvider(DateTimeOffset utcNow)
        : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class TestHostEnvironment(string contentRootPath)
        : IHostEnvironment
    {
        public string EnvironmentName { get; set; } =
            Environments.Development;
        public string ApplicationName { get; set; } = "DataRights.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }

    private sealed class TestDirectory : IAsyncDisposable
    {
        public TestDirectory() =>
            this.Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"bunkfy-tenant-termination-replay-{Guid.NewGuid():N}");

        public string Path { get; }

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
