namespace BunkFy.Adapters.Tests;

using System.Net;
using System.Text.Json;
using BunkFy.AdapterHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterHostTests
{
    [Fact]
    public async Task Host_acquires_checkpoint_lease_and_exposes_only_factual_non_sensitive_status()
    {
        string directory = CreateDirectory();
        try
        {
            string configurationPath = Path.Combine(directory, "adapter.json");
            string tokenPath = Path.Combine(directory, "token.txt");
            string checkpointPath = Path.Combine(directory, "checkpoint.json");
            await File.WriteAllTextAsync(configurationPath, "{}");
            await File.WriteAllTextAsync(tokenPath, "bfi_v1_test-token");
            await using TestAdapterHost application = new(
                configurationPath,
                tokenPath,
                checkpointPath);
            using HttpClient client = application.CreateClient();

            using HttpResponseMessage live = await client.GetAsync("/health/live");
            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            HttpResponseMessage? ready = null;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                ready?.Dispose();
                ready = await client.GetAsync("/health/ready");
                if (ready.StatusCode == HttpStatusCode.OK)
                {
                    break;
                }

                await Task.Delay(25);
            }

            using (ready)
            {
                Assert.NotNull(ready);
                Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            }

            using HttpResponseMessage response = await client.GetAsync("/status");
            string statusJson = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using JsonDocument status = JsonDocument.Parse(statusJson);
            Assert.Equal("fake.http", status.RootElement.GetProperty("adapterType").GetString());
            Assert.True(status.RootElement.GetProperty("ready").GetBoolean());
            Assert.False(status.RootElement.GetProperty("hasCheckpoint").GetBoolean());
            Assert.False(status.RootElement.TryGetProperty("tenantId", out _));
            Assert.False(status.RootElement.TryGetProperty("propertyId", out _));
            Assert.False(status.RootElement.TryGetProperty("checkpoint", out _));
            Assert.DoesNotContain("token", statusJson, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(directory, statusJson, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(checkpointPath + ".lock"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Token_file_is_reloaded_for_rotation_and_never_rendered()
    {
        string directory = CreateDirectory();
        try
        {
            string configurationPath = Path.Combine(directory, "adapter.json");
            string tokenPath = Path.Combine(directory, "token.txt");
            await File.WriteAllTextAsync(configurationPath, "{}");
            await File.WriteAllTextAsync(tokenPath, "bfi_v1_first");
            AdapterHostOptions options = CreateOptions(configurationPath, tokenPath, Path.Combine(
                directory, "checkpoint.json"));
            ReloadingAdapterIngressTokenProvider provider = new(options);

            string first = await provider.GetTokenAsync(CancellationToken.None);
            await File.WriteAllTextAsync(tokenPath, "bfi_v1_second");
            string second = await provider.GetTokenAsync(CancellationToken.None);

            Assert.Equal("bfi_v1_first", first);
            Assert.Equal("bfi_v1_second", second);
            Assert.Equal(nameof(ReloadingAdapterIngressTokenProvider), provider.ToString());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void Options_reject_ambiguous_token_sources_and_insecure_remote_ingress()
    {
        Dictionary<string, string?> ambiguous = ValidValues();
        ambiguous["AdapterHost:IngressTokenEnvironmentVariable"] = "BUNKFY_TOKEN";
        ambiguous["AdapterHost:IngressTokenFilePath"] = "token.txt";
        Assert.Throws<InvalidOperationException>(() => AdapterHostOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(ambiguous).Build()));

        Dictionary<string, string?> insecure = ValidValues();
        insecure["AdapterHost:IngressTokenEnvironmentVariable"] = "BUNKFY_TOKEN";
        insecure["AdapterHost:ServiceBaseAddress"] = "http://example.test";
        Assert.Throws<ArgumentException>(() => AdapterHostOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(insecure).Build()));
    }

    [Fact]
    public void Options_validate_and_expose_file_drop_retention_policy()
    {
        Dictionary<string, string?> values = ValidValues();
        values["AdapterHost:AdapterType"] = "json.file-drop";
        values["AdapterHost:JsonFileDropRoot"] = "file-drop";
        values["AdapterHost:IngressTokenEnvironmentVariable"] = "BUNKFY_TOKEN";
        values["AdapterHost:JsonFileDropRetentionEnabled"] = "false";
        values["AdapterHost:JsonFileDropProcessedArchiveRetention"] = "2.00:00:00";
        values["AdapterHost:JsonFileDropFailedQuarantineRetention"] = "5.00:00:00";
        values["AdapterHost:JsonFileDropMaximumDeletesPerRun"] = "7";

        AdapterHostOptions options = AdapterHostOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        Assert.False(options.JsonFileDropRetentionEnabled);
        Assert.Equal(TimeSpan.FromDays(2), options.JsonFileDropProcessedArchiveRetention);
        Assert.Equal(TimeSpan.FromDays(5), options.JsonFileDropFailedQuarantineRetention);
        Assert.Equal(7, options.JsonFileDropMaximumDeletesPerRun);

        values["AdapterHost:JsonFileDropMaximumDeletesPerRun"] = "0";
        Assert.Throws<ArgumentOutOfRangeException>(() => AdapterHostOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        values.Remove("AdapterHost:JsonFileDropRoot");
        values["AdapterHost:JsonFileDropMaximumDeletesPerRun"] = "7";
        Assert.Throws<InvalidOperationException>(() => AdapterHostOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
    }

    [Fact]
    public void Server_lease_options_require_worker_identity_and_do_not_require_local_checkpoint_state()
    {
        Dictionary<string, string?> values = ValidValues();
        values["AdapterHost:CoordinationMode"] = "server-lease";
        values["AdapterHost:WorkerId"] = "30000000-0000-0000-0000-000000000001";
        values["AdapterHost:RemoteLeaseDuration"] = "00:01:00";
        values["AdapterHost:IngressTokenEnvironmentVariable"] = "BUNKFY_TOKEN";
        values.Remove("AdapterHost:CheckpointFilePath");

        AdapterHostOptions options = AdapterHostOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());

        Assert.Equal(AdapterHostCoordinationMode.ServerLease, options.CoordinationMode);
        Assert.Equal(Guid.Parse("30000000-0000-0000-0000-000000000001"), options.WorkerId);
        Assert.Null(options.CheckpointFilePath);
        Assert.Equal(TimeSpan.FromMinutes(1), options.RemoteLeaseDuration);

        values.Remove("AdapterHost:WorkerId");
        Assert.Throws<InvalidOperationException>(() => AdapterHostOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
        values["AdapterHost:WorkerId"] = "30000000-0000-0000-0000-000000000001";
        values["AdapterHost:RemoteLeaseDuration"] = "00:00:29";
        Assert.Throws<InvalidOperationException>(() => AdapterHostOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build()));
    }

    [Fact]
    public void Lease_contention_is_visible_without_inflating_runtime_failures()
    {
        AdapterHostStatus status = new(CreateOptions("adapter.json", "token.txt", "checkpoint.json"));
        DateTimeOffset now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        status.SetReady(hasCheckpoint: false, now);
        status.SetRunning(now);

        status.SetLeaseUnavailable(now.AddSeconds(1), now.AddMinutes(5));

        AdapterHostStatusSnapshot snapshot = status.Snapshot();
        Assert.Equal(AdapterHostCycleState.Delaying, snapshot.State);
        Assert.Null(snapshot.LastOutcome);
        Assert.Equal("adapter.remote-lease-unavailable", snapshot.LastErrorCode);
        Assert.Equal(0, snapshot.ConsecutiveFailures);
        Assert.Equal(now.AddMinutes(5), snapshot.NextCycleAtUtc);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("SOURCE.TIMEOUT_1", "source.timeout_1")]
    [InlineData("provider failed: secret=value", "adapter.provider-failed")]
    [InlineData("line\nbreak", "adapter.provider-failed")]
    public void Status_error_codes_are_normalized_and_never_render_free_form_provider_text(
        string? errorCode,
        string? expected)
    {
        Assert.Equal(expected, StandaloneAdapterPollingService.SafeErrorCode(errorCode));
    }

    private static AdapterHostOptions CreateOptions(
        string configurationPath,
        string tokenPath,
        string checkpointPath)
    {
        Dictionary<string, string?> values = ValidValues();
        values["AdapterHost:ConfigurationFilePath"] = configurationPath;
        values["AdapterHost:CheckpointFilePath"] = checkpointPath;
        values["AdapterHost:IngressTokenFilePath"] = tokenPath;
        return AdapterHostOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build());
    }

    private static Dictionary<string, string?> ValidValues() => new()
    {
        ["AdapterHost:AdapterType"] = "fake.http",
        ["AdapterHost:TenantId"] = "tenant-a",
        ["AdapterHost:PropertyId"] = "10000000-0000-0000-0000-000000000001",
        ["AdapterHost:ConnectionId"] = "20000000-0000-0000-0000-000000000001",
        ["AdapterHost:CoordinationMode"] = "local-file",
        ["AdapterHost:ServiceBaseAddress"] = "http://localhost:7001",
        ["AdapterHost:CheckpointFilePath"] = "checkpoint.json",
        ["AdapterHost:ConfigurationFilePath"] = "adapter.json",
        ["AdapterHost:ConfigurationContentType"] = "application/json",
        ["AdapterHost:PollInterval"] = "01:00:00",
        ["AdapterHost:MaximumRunDuration"] = "00:05:00",
        ["AdapterHost:RetryBaseDelay"] = "00:00:01",
        ["AdapterHost:RetryMaxDelay"] = "00:00:10",
        ["AdapterHost:RunOnStart"] = "false",
        ["AdapterHost:AllowInsecureLoopback"] = "true",
        ["AdapterHost:ListenUrl"] = "http://127.0.0.1:0"
    };

    private static string CreateDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"bunkfy-adapter-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class TestAdapterHost(
        string configurationPath,
        string tokenPath,
        string checkpointPath)
        : WebApplicationFactory<AdapterHostAssemblyReference>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Integration");
            foreach ((string key, string? value) in ValidValues())
            {
                builder.UseSetting(key, value);
            }

            builder.UseSetting("AdapterHost:ConfigurationFilePath", configurationPath);
            builder.UseSetting("AdapterHost:CheckpointFilePath", checkpointPath);
            builder.UseSetting("AdapterHost:IngressTokenEnvironmentVariable", string.Empty);
            builder.UseSetting("AdapterHost:IngressTokenFilePath", tokenPath);
        }
    }
}
