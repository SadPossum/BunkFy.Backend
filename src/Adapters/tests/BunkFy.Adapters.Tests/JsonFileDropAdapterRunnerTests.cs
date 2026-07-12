namespace BunkFy.Adapters.Tests;

using System.Text;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using BunkFy.Adapters.JsonFileDrop;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class JsonFileDropAdapterRunnerTests
{
    private static readonly Guid ConnectionId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse(
        "2026-07-12T12:00:00Z",
        System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Archives_sorted_durable_files_and_replays_with_the_same_operation_id()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        await File.WriteAllTextAsync(Path.Combine(pending, "b.json"), Envelope("booking-b", "2"));
        await File.WriteAllTextAsync(Path.Combine(pending, "a.json"), Envelope("booking-a", "1"));
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        RecordingSink sink = new(submission => Acknowledge(
            submission,
            AdapterObservationDisposition.Accepted,
            checkpointAccepted: true));

        AdapterRunCompletion first = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);
        Guid firstOperationId = sink.Submissions[0].Records.First().OperationId;

        Assert.Equal(AdapterRunOutcome.Succeeded, first.Outcome);
        Assert.Equal(2, first.AcceptedCount);
        Assert.Equal("b.json", first.AcceptedCheckpoint);
        Assert.Equal(
            ["booking-a", "booking-b"],
            sink.Submissions[0].Records.Select(record => record.ExternalRecordId));
        Assert.Empty(Directory.EnumerateFiles(pending, "*.json"));
        string processed = GetProcessed(directory.Path);
        Assert.Equal(2, Directory.EnumerateFiles(processed, "*.json").Count());

        File.Copy(Path.Combine(processed, "a.json"), Path.Combine(pending, "a.json"));
        sink.Submissions.Clear();
        sink.Acknowledger = submission => Acknowledge(
            submission,
            AdapterObservationDisposition.Duplicate,
            checkpointAccepted: true);
        AdapterRunCompletion replay = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Succeeded, replay.Outcome);
        Assert.Equal(firstOperationId, Assert.Single(sink.Submissions).Records.First().OperationId);
        Assert.False(File.Exists(Path.Combine(pending, "a.json")));
    }

    [Fact]
    public async Task Archives_durable_sibling_but_keeps_rejected_file_pending()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        await File.WriteAllTextAsync(Path.Combine(pending, "a.json"), Envelope("accepted", "1"));
        await File.WriteAllTextAsync(Path.Combine(pending, "b.json"), Envelope("rejected", "2"));
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        RecordingSink sink = new(submission =>
        {
            AdapterObservedRecord[] records = submission.Records.ToArray();
            return new AdapterObservationAcknowledgement(
                submission.RunId,
                submission.LeaseId,
                [
                    Result(records[0], AdapterObservationDisposition.Accepted),
                    Result(records[1], AdapterObservationDisposition.Rejected)
                ],
                checkpointAccepted: false,
                acceptedCheckpoint: null);
        });

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Equal(1, completion.AcceptedCount);
        Assert.Equal(1, completion.RejectedCount);
        Assert.False(File.Exists(Path.Combine(pending, "a.json")));
        Assert.True(File.Exists(Path.Combine(pending, "b.json")));
        Assert.True(File.Exists(Path.Combine(GetProcessed(directory.Path), "a.json")));
    }

    [Fact]
    public async Task Acknowledgement_mismatch_moves_nothing()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        await File.WriteAllTextAsync(Path.Combine(pending, "input.json"), Envelope("booking-1", "1"));
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        RecordingSink sink = new(submission => new AdapterObservationAcknowledgement(
            submission.RunId,
            submission.LeaseId,
            [new AdapterObservationResult(
                Guid.NewGuid(),
                AdapterObservationDisposition.Accepted,
                Guid.NewGuid(),
                errorCode: null)],
            checkpointAccepted: true,
            submission.ProposedCheckpoint));

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Failed, completion.Outcome);
        Assert.Equal("json-file-drop.acknowledgement-mismatch", completion.ErrorCode);
        Assert.True(File.Exists(Path.Combine(pending, "input.json")));
        Assert.Empty(Directory.EnumerateFiles(GetProcessed(directory.Path), "*.json"));
    }

    [Fact]
    public async Task Quarantines_unknown_envelope_fields_with_safe_metadata_and_allows_repair()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string path = Path.Combine(pending, "invalid.json");
        await File.WriteAllTextAsync(path, Envelope("booking-1", "1")[..^1] + ",\"unknown\":true}");
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        RecordingSink sink = new(_ => throw new InvalidOperationException("must not submit"));

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Equal("json-file-drop.input-quarantined", completion.ErrorCode);
        Assert.Empty(sink.Submissions);
        Assert.False(File.Exists(path));
        string failed = GetFailed(directory.Path);
        string failedInput = Assert.Single(
            Directory.EnumerateFiles(failed, "*.json"),
            item => !item.EndsWith(".failure.json", StringComparison.Ordinal));
        string metadataPath = Assert.Single(Directory.EnumerateFiles(failed, "*.failure.json"));
        string metadata = await File.ReadAllTextAsync(metadataPath);
        using JsonDocument document = JsonDocument.Parse(metadata);
        Assert.Equal("invalid.json", document.RootElement.GetProperty("originalFileName").GetString());
        Assert.Equal("json-file-drop.invalid-json", document.RootElement.GetProperty("errorCode").GetString());
        Assert.DoesNotContain(directory.Path, metadata, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unknown", metadata, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("unknown", await File.ReadAllTextAsync(failedInput), StringComparison.Ordinal);

        await File.WriteAllTextAsync(path, Envelope("booking-1", "2"));
        sink.Acknowledger = submission => Acknowledge(
            submission,
            AdapterObservationDisposition.Accepted,
            checkpointAccepted: true);
        AdapterRunCompletion repaired = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);
        Assert.Equal(AdapterRunOutcome.Succeeded, repaired.Outcome);
        Assert.False(File.Exists(path));
        Assert.True(File.Exists(Path.Combine(GetProcessed(directory.Path), "invalid.json")));
    }

    [Fact]
    public async Task Quarantines_oversized_envelope_before_submission()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string path = Path.Combine(pending, "oversized.json");
        await using (FileStream file = File.Create(path))
        {
            file.SetLength(AdapterProtocolLimits.MaximumInlinePayloadBytes + (64 * 1024) + 1L);
        }

        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());

        RecordingSink sink = new(_ => throw new InvalidOperationException("must not submit"));
        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Empty(sink.Submissions);
        Assert.False(File.Exists(path));
        Assert.Contains(
            Directory.EnumerateFiles(GetFailed(directory.Path), "*.failure.json"),
            item => File.ReadAllText(item).Contains("json-file-drop.envelope-too-large", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Poison_file_does_not_block_valid_later_sibling()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        await File.WriteAllTextAsync(Path.Combine(pending, "a.json"), "{ invalid");
        await File.WriteAllTextAsync(Path.Combine(pending, "b.json"), Envelope("booking-valid", "1"));
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        RecordingSink sink = new(submission => Acknowledge(
            submission,
            AdapterObservationDisposition.Accepted,
            checkpointAccepted: true));

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Equal(1, completion.ObservedCount);
        Assert.Equal(1, completion.AcceptedCount);
        Assert.Equal("booking-valid", Assert.Single(sink.Submissions).Records.Single().ExternalRecordId);
        Assert.Empty(Directory.EnumerateFiles(pending, "*.json"));
        Assert.True(File.Exists(Path.Combine(GetProcessed(directory.Path), "b.json")));
        Assert.True(File.Exists(Path.Combine(GetFailed(directory.Path), "a.json")));
    }

    [Fact]
    public async Task Transient_read_sharing_failure_quarantines_nothing()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string path = Path.Combine(pending, "locked.json");
        await File.WriteAllTextAsync(path, Envelope("booking-locked", "1"));
        using FileStream lockStream = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(
                CreateAssignment(),
                CreateMaterial(),
                new RecordingSink(_ => throw new InvalidOperationException("must not submit")),
                CancellationToken.None));

        Assert.Equal("The JSON file-drop input could not be read.", exception.Message);
        Assert.True(File.Exists(path));
        Assert.Empty(Directory.EnumerateFiles(GetFailed(directory.Path)));
    }

    [Fact]
    public async Task Stops_batch_before_aggregate_limit_without_starving_valid_files()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string data = new('x', 3_500_000);
        foreach (char name in "abcde")
        {
            await File.WriteAllTextAsync(
                Path.Combine(pending, $"{name}.json"),
                LargeEnvelope($"booking-{name}", data));
        }

        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        RecordingSink sink = new(submission => Acknowledge(
            submission,
            AdapterObservationDisposition.Accepted,
            checkpointAccepted: true));

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Succeeded, completion.Outcome);
        Assert.Equal(4, completion.AcceptedCount);
        Assert.Equal(4, Assert.Single(sink.Submissions).Records.Count);
        Assert.Equal(["e.json"], Directory.EnumerateFiles(pending).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Rejects_symbolic_link_input()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string target = Path.Combine(directory.Path, "outside-envelope.tmp");
        await File.WriteAllTextAsync(target, Envelope("booking-link", "1"));
        string link = Path.Combine(pending, "linked.json");
        try
        {
            File.CreateSymbolicLink(link, target);
        }
        catch (IOException) when (OperatingSystem.IsWindows())
        {
            // Windows requires Developer Mode or SeCreateSymbolicLinkPrivilege for this fixture.
            return;
        }
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());

        RecordingSink sink = new(_ => throw new InvalidOperationException("must not submit"));
        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Empty(sink.Submissions);
        Assert.False(File.Exists(link));
        Assert.True(File.Exists(target));
        string quarantinedLink = Path.Combine(GetFailed(directory.Path), "linked.json");
        Assert.Equal(
            FileAttributes.ReparsePoint,
            File.GetAttributes(quarantinedLink) & FileAttributes.ReparsePoint);
    }

    [Fact]
    public async Task Detects_input_change_after_durable_submission()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string path = Path.Combine(pending, "changing.json");
        await File.WriteAllTextAsync(path, Envelope("booking-1", "1"));
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        RecordingSink sink = new(submission =>
        {
            File.WriteAllText(path, Envelope("booking-changed", "2"));
            return Acknowledge(submission, AdapterObservationDisposition.Accepted, checkpointAccepted: true);
        });

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(CreateAssignment(), CreateMaterial(), sink, CancellationToken.None));

        Assert.Equal("The JSON file-drop input changed while it was being processed.", exception.Message);
        Assert.True(File.Exists(path));
        Assert.Empty(Directory.EnumerateFiles(GetProcessed(directory.Path), "*.json"));
    }

    [Fact]
    public async Task Conflicting_archive_file_does_not_overwrite_or_remove_input()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string pendingPath = Path.Combine(pending, "collision.json");
        await File.WriteAllTextAsync(pendingPath, Envelope("booking-new", "2"));
        string processed = GetProcessed(directory.Path);
        Directory.CreateDirectory(processed);
        string processedPath = Path.Combine(processed, "collision.json");
        await File.WriteAllTextAsync(processedPath, Envelope("booking-old", "1"));
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunAsync(
                CreateAssignment(),
                CreateMaterial(),
                new RecordingSink(submission => Acknowledge(
                    submission,
                    AdapterObservationDisposition.Accepted,
                    checkpointAccepted: true)),
                CancellationToken.None));

        Assert.Equal("The JSON file-drop archive contains a conflicting filename.", exception.Message);
        Assert.True(File.Exists(pendingPath));
        Assert.Contains("booking-old", await File.ReadAllTextAsync(processedPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Quarantine_name_collision_preserves_both_raw_inputs()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string failed = GetFailed(directory.Path);
        Directory.CreateDirectory(failed);
        await File.WriteAllTextAsync(Path.Combine(failed, "invalid.json"), "first invalid artifact");
        await File.WriteAllTextAsync(Path.Combine(pending, "invalid.json"), "{ second invalid artifact");
        using ServiceProvider provider = CreateProvider(directory.Path);
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(),
            CreateMaterial(),
            new RecordingSink(_ => throw new InvalidOperationException("must not submit")),
            CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        string[] rawArtifacts = Directory.EnumerateFiles(failed, "*.json")
            .Where(item => !item.EndsWith(".failure.json", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(2, rawArtifacts.Length);
        Assert.Contains(rawArtifacts, item => File.ReadAllText(item).Contains("first invalid", StringComparison.Ordinal));
        Assert.Contains(rawArtifacts, item => File.ReadAllText(item).Contains("second invalid", StringComparison.Ordinal));
        Assert.Single(Directory.EnumerateFiles(failed, "*.failure.json"));
    }

    [Fact]
    public async Task Archive_timestamp_is_adapter_owned_and_expired_processed_files_are_removed()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string pendingPath = Path.Combine(pending, "fresh.json");
        await File.WriteAllTextAsync(pendingPath, Envelope("fresh", "1"));
        File.SetLastWriteTimeUtc(pendingPath, Now.AddYears(-1).UtcDateTime);
        string processed = GetProcessed(directory.Path);
        Directory.CreateDirectory(processed);
        string expired = Path.Combine(processed, "expired.json");
        string current = Path.Combine(processed, "current.json");
        string unknown = Path.Combine(processed, "operator-note.txt");
        await File.WriteAllTextAsync(expired, "expired");
        await File.WriteAllTextAsync(current, "current");
        await File.WriteAllTextAsync(unknown, "unknown");
        File.SetLastWriteTimeUtc(expired, Now.AddDays(-8).UtcDateTime);
        File.SetLastWriteTimeUtc(current, Now.AddDays(-6).UtcDateTime);
        File.SetLastWriteTimeUtc(unknown, Now.AddYears(-1).UtcDateTime);
        JsonFileDropAdapterRunner runner = CreateRunner(directory.Path);
        RecordingSink sink = new(submission => Acknowledge(
            submission,
            AdapterObservationDisposition.Accepted,
            checkpointAccepted: true));

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Succeeded, completion.Outcome);
        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(current));
        Assert.True(File.Exists(unknown));
        string archived = Path.Combine(processed, "fresh.json");
        Assert.Equal(Now, new DateTimeOffset(File.GetLastWriteTimeUtc(archived), TimeSpan.Zero));
    }

    [Fact]
    public async Task Expired_quarantine_pair_and_interrupted_sidecar_are_removed_but_orphans_are_preserved()
    {
        using TemporaryDirectory directory = new();
        CreatePending(directory.Path);
        WriteQuarantine(directory.Path, "pair.json", Now.AddDays(-31), includeRaw: true);
        WriteQuarantine(directory.Path, "sidecar-only.json", Now.AddDays(-31), includeRaw: false);
        string failed = GetFailed(directory.Path);
        string orphanRaw = Path.Combine(failed, "orphan.json");
        string malformed = Path.Combine(failed, "malformed.json.failure.json");
        await File.WriteAllTextAsync(orphanRaw, "operator-owned orphan");
        await File.WriteAllTextAsync(malformed, "{ invalid");
        JsonFileDropAdapterRunner runner = CreateRunner(directory.Path);

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(),
            CreateMaterial(),
            new RecordingSink(_ => throw new InvalidOperationException("must not submit")),
            CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, completion.Outcome);
        Assert.Equal("json-file-drop.retention-maintenance-failed", completion.ErrorCode);
        Assert.False(File.Exists(Path.Combine(failed, "pair.json")));
        Assert.False(File.Exists(Path.Combine(failed, "pair.json.failure.json")));
        Assert.False(File.Exists(Path.Combine(failed, "sidecar-only.json.failure.json")));
        Assert.True(File.Exists(orphanRaw));
        Assert.True(File.Exists(malformed));
        Assert.DoesNotContain(directory.Path, completion.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Processed_and_failed_categories_have_independent_deletion_budgets()
    {
        using TemporaryDirectory directory = new();
        CreatePending(directory.Path);
        string processed = GetProcessed(directory.Path);
        Directory.CreateDirectory(processed);
        foreach (string name in new[] { "one.json", "two.json" })
        {
            string path = Path.Combine(processed, name);
            await File.WriteAllTextAsync(path, name);
            File.SetLastWriteTimeUtc(path, Now.AddDays(-8).UtcDateTime);
        }

        WriteQuarantine(directory.Path, "one.json", Now.AddDays(-31), includeRaw: true);
        WriteQuarantine(directory.Path, "two.json", Now.AddDays(-31), includeRaw: true);
        JsonFileDropAdapterRunner runner = CreateRunner(directory.Path, maximumDeletesPerRun: 1);

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(),
            CreateMaterial(),
            new RecordingSink(_ => throw new InvalidOperationException("must not submit")),
            CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Succeeded, completion.Outcome);
        Assert.Single(Directory.EnumerateFiles(processed, "*.json"));
        Assert.Single(Directory.EnumerateFiles(GetFailed(directory.Path), "*.failure.json"));
        Assert.Single(
            Directory.EnumerateFiles(GetFailed(directory.Path), "*.json"),
            path => !path.EndsWith(".failure.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Maintenance_failure_does_not_block_valid_source_and_preserves_primary_source_error()
    {
        using TemporaryDirectory directory = new();
        string pending = CreatePending(directory.Path);
        string failed = GetFailed(directory.Path);
        Directory.CreateDirectory(failed);
        await File.WriteAllTextAsync(Path.Combine(failed, "malformed.json.failure.json"), "{ invalid");
        await File.WriteAllTextAsync(Path.Combine(pending, "valid.json"), Envelope("valid", "1"));
        JsonFileDropAdapterRunner runner = CreateRunner(directory.Path);
        RecordingSink sink = new(submission => Acknowledge(
            submission,
            AdapterObservationDisposition.Accepted,
            checkpointAccepted: true));

        AdapterRunCompletion accepted = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, accepted.Outcome);
        Assert.Equal("json-file-drop.retention-maintenance-failed", accepted.ErrorCode);
        Assert.Equal(1, accepted.AcceptedCount);
        Assert.True(File.Exists(Path.Combine(GetProcessed(directory.Path), "valid.json")));

        await File.WriteAllTextAsync(Path.Combine(pending, "invalid.json"), "{ invalid");
        AdapterRunCompletion quarantined = await runner.RunAsync(
            CreateAssignment(), CreateMaterial(), sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.PartiallySucceeded, quarantined.Outcome);
        Assert.Equal("json-file-drop.input-quarantined", quarantined.ErrorCode);
        Assert.Contains("retention item", quarantined.ErrorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Retention_can_be_paused_without_deleting_expired_artifacts()
    {
        using TemporaryDirectory directory = new();
        CreatePending(directory.Path);
        string processed = GetProcessed(directory.Path);
        Directory.CreateDirectory(processed);
        string expired = Path.Combine(processed, "expired.json");
        await File.WriteAllTextAsync(expired, "expired");
        File.SetLastWriteTimeUtc(expired, Now.AddDays(-8).UtcDateTime);
        WriteQuarantine(directory.Path, "failed.json", Now.AddDays(-31), includeRaw: true);
        JsonFileDropAdapterRunner runner = CreateRunner(directory.Path, retentionEnabled: false);

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment(),
            CreateMaterial(),
            new RecordingSink(_ => throw new InvalidOperationException("must not submit")),
            CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Succeeded, completion.Outcome);
        Assert.True(File.Exists(expired));
        Assert.True(File.Exists(Path.Combine(GetFailed(directory.Path), "failed.json")));
    }

    [Theory]
    [InlineData(0, 30, 100)]
    [InlineData(7, 0, 100)]
    [InlineData(7, 30, 0)]
    [InlineData(7, 30, 1001)]
    public void Options_reject_invalid_retention_policy(
        int processedDays,
        int failedDays,
        int maximumDeletes)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new JsonFileDropAdapterOptions(
            "root",
            TimeSpan.FromDays(processedDays),
            TimeSpan.FromDays(failedDays),
            maximumDeletes));
    }

    [Fact]
    public void Registration_separates_descriptor_from_runner_and_redacts_root()
    {
        ServiceCollection descriptors = new();
        descriptors.AddJsonFileDropAdapterDescriptor();
        using ServiceProvider descriptorProvider = descriptors.BuildServiceProvider();
        Assert.Single(descriptorProvider.GetServices<IAdapterDescriptorProvider>());
        Assert.Empty(descriptorProvider.GetServices<IAdapterRunner>());

        JsonFileDropAdapterOptions options = new(Path.Combine("private", "adapter", "root"));
        using ServiceProvider runnerProvider = CreateProvider(options.RootPath);
        Assert.Single(runnerProvider.GetServices<IAdapterDescriptorProvider>());
        Assert.Single(runnerProvider.GetServices<IAdapterRunner>());
        Assert.Equal(nameof(JsonFileDropAdapterOptions), options.ToString());
        Assert.DoesNotContain(options.RootPath, options.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static ServiceProvider CreateProvider(string root)
    {
        ServiceCollection services = new();
        services.AddJsonFileDropAdapter(new JsonFileDropAdapterOptions(root));
        return services.BuildServiceProvider();
    }

    private static JsonFileDropAdapterRunner CreateRunner(
        string root,
        int maximumDeletesPerRun = JsonFileDropAdapterOptions.DefaultMaximumDeletesPerRun,
        bool retentionEnabled = true) => new(
        new JsonFileDropAdapterOptions(
            root,
            JsonFileDropAdapterOptions.DefaultProcessedArchiveRetention,
            JsonFileDropAdapterOptions.DefaultFailedQuarantineRetention,
            maximumDeletesPerRun,
            retentionEnabled),
        new FixedTimeProvider(Now));

    private static void WriteQuarantine(
        string root,
        string fileName,
        DateTimeOffset quarantinedAtUtc,
        bool includeRaw)
    {
        string failed = GetFailed(root);
        Directory.CreateDirectory(failed);
        string rawPath = Path.Combine(failed, fileName);
        if (includeRaw)
        {
            File.WriteAllText(rawPath, "failed source");
        }

        File.WriteAllText(rawPath + ".failure.json", JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            originalFileName = fileName,
            errorCode = "json-file-drop.invalid-json",
            quarantinedAtUtc
        }));
    }

    private static string CreatePending(string root)
    {
        string path = Path.Combine(root, ConnectionId.ToString("N"), "pending");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetProcessed(string root) =>
        Path.Combine(root, ConnectionId.ToString("N"), "processed");

    private static string GetFailed(string root) =>
        Path.Combine(root, ConnectionId.ToString("N"), "failed");

    private static AdapterRunAssignment CreateAssignment() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        ConnectionId,
        "tenant-a",
        Guid.Parse("40000000-0000-0000-0000-000000000002"),
        JsonFileDropAdapterDescriptor.AdapterType,
        AdapterExecutionMode.Polling,
        DateTimeOffset.Parse("2026-07-12T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        DateTimeOffset.Parse("2026-07-12T12:05:00Z", System.Globalization.CultureInfo.InvariantCulture),
        checkpoint: null);

    private static AdapterConfigurationMaterial CreateMaterial() => new(
        schemaVersion: 1,
        "application/json",
        "{}"u8,
        secretContentType: null,
        []);

    private static AdapterObservationAcknowledgement Acknowledge(
        AdapterObservationSubmission submission,
        AdapterObservationDisposition disposition,
        bool checkpointAccepted) => new(
            submission.RunId,
            submission.LeaseId,
            submission.Records.Select(record => Result(record, disposition)).ToArray(),
            checkpointAccepted,
            checkpointAccepted ? submission.ProposedCheckpoint : null);

    private static AdapterObservationResult Result(
        AdapterObservedRecord record,
        AdapterObservationDisposition disposition) => new(
            record.OperationId,
            disposition,
            disposition == AdapterObservationDisposition.Rejected ? null : Guid.NewGuid(),
            disposition == AdapterObservationDisposition.Rejected ? "test.rejected" : null);

    private static string Envelope(string externalId, string revision) => $$"""
        {
          "schemaVersion": 1,
          "recordType": "reservation.v1",
          "externalRecordId": "{{externalId}}",
          "sourceRevision": "{{revision}}",
          "sourceUpdatedAtUtc": "2026-07-12T11:59:00Z",
          "observedAtUtc": "2026-07-12T12:00:00Z",
          "payload": { "status": "confirmed", "externalId": "{{externalId}}" }
        }
        """;

    private static string LargeEnvelope(string externalId, string data) => $$"""
        {
          "schemaVersion": 1,
          "recordType": "large-test.v1",
          "externalRecordId": "{{externalId}}",
          "sourceRevision": "1",
          "sourceUpdatedAtUtc": "2026-07-12T11:59:00Z",
          "observedAtUtc": "2026-07-12T12:00:00Z",
          "payload": { "data": "{{data}}" }
        }
        """;

    private sealed class RecordingSink(
        Func<AdapterObservationSubmission, AdapterObservationAcknowledgement> acknowledger)
        : IAdapterObservationSink
    {
        public Func<AdapterObservationSubmission, AdapterObservationAcknowledgement> Acknowledger { get; set; } =
            acknowledger;

        public List<AdapterObservationSubmission> Submissions { get; } = [];

        public Task<AdapterObservationAcknowledgement> SubmitAsync(
            AdapterObservationSubmission submission,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Submissions.Add(submission);
            return Task.FromResult(this.Acknowledger(submission));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            this.Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"bunkfy-json-file-drop-{Guid.NewGuid():N}");
            Directory.CreateDirectory(this.Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, recursive: true);
            }
        }
    }
}
