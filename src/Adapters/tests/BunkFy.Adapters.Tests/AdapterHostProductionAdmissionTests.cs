namespace BunkFy.Adapters.Tests;

using System.Net;
using BunkFy.Adapter.Abstractions;
using BunkFy.Adapter.Runtime;
using BunkFy.Adapters.Http;
using BunkFy.AdapterHost;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterHostProductionAdmissionTests
{
    [Fact]
    public void Non_production_bypasses_pending_admission()
    {
        IConfiguration configuration = Configuration(ValidValues());

        string[] failures = AdapterHostProductionAdmission.Validate(
            AdapterHostProductionAdmissionOptions.FromConfiguration(
                configuration),
            AdapterHostOptions.FromConfiguration(configuration),
            isProduction: false);

        Assert.Empty(failures);
    }

    [Fact]
    public void Production_accepts_approved_remote_process()
    {
        IConfiguration configuration = Configuration(ApprovedValues());

        string[] failures = AdapterHostProductionAdmission.Validate(
            AdapterHostProductionAdmissionOptions.FromConfiguration(
                configuration),
            AdapterHostOptions.FromConfiguration(configuration),
            isProduction: true);

        Assert.Empty(failures);
    }

    [Fact]
    public void Production_rejects_pending_local_and_insecure_configuration()
    {
        IConfiguration configuration = Configuration(ValidValues());

        string[] failures = AdapterHostProductionAdmission.Validate(
            AdapterHostProductionAdmissionOptions.FromConfiguration(
                configuration),
            AdapterHostOptions.FromConfiguration(configuration),
            isProduction: true);

        Assert.Contains(failures, failure =>
            failure.Contains("ApprovalState", StringComparison.Ordinal));
        Assert.Contains(failures, failure =>
            failure.Contains("server-lease", StringComparison.Ordinal));
        Assert.Contains(failures, failure =>
            failure.Contains("AllowInsecureLoopback=false", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_unapproved_adapter_and_service_base()
    {
        Dictionary<string, string?> values = ApprovedValues();
        values["AdapterHost:ProductionAdmission:ApprovedAdapterType"] =
            "imap.reservation-mail";
        values["AdapterHost:ProductionAdmission:ApprovedServiceBaseAddress"] =
            "https://other.example.test/";
        IConfiguration configuration = Configuration(values);

        string[] failures = AdapterHostProductionAdmission.Validate(
            AdapterHostProductionAdmissionOptions.FromConfiguration(
                configuration),
            AdapterHostOptions.FromConfiguration(configuration),
            isProduction: true);

        Assert.Contains(failures, failure =>
            failure.Contains("ApprovedAdapterType", StringComparison.Ordinal));
        Assert.Contains(failures, failure =>
            failure.Contains(
                "ApprovedServiceBaseAddress",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Production_container_requires_an_immutable_image_digest()
    {
        Dictionary<string, string?> values = ApprovedValues();
        values["AdapterHost:ProductionAdmission:Runtime"] = "Container";
        IConfiguration configuration = Configuration(values);

        string[] failures = AdapterHostProductionAdmission.Validate(
            AdapterHostProductionAdmissionOptions.FromConfiguration(
                configuration),
            AdapterHostOptions.FromConfiguration(configuration),
            isProduction: true);

        Assert.Contains(failures, failure =>
            failure.Contains("ContainerImageDigest", StringComparison.Ordinal));

        values["AdapterHost:ProductionAdmission:ContainerImageDigest"] =
            "sha256:" + new string('2', 64);
        configuration = Configuration(values);
        Assert.Empty(AdapterHostProductionAdmission.Validate(
            AdapterHostProductionAdmissionOptions.FromConfiguration(
                configuration),
            AdapterHostOptions.FromConfiguration(configuration),
            isProduction: true));
    }

    [Fact]
    public void Loopback_status_rejects_a_non_loopback_listener()
    {
        Dictionary<string, string?> values = ApprovedValues();
        values["AdapterHost:ListenUrl"] = "http://0.0.0.0:8088";
        IConfiguration configuration = Configuration(values);

        string[] failures = AdapterHostProductionAdmission.Validate(
            AdapterHostProductionAdmissionOptions.FromConfiguration(
                configuration),
            AdapterHostOptions.FromConfiguration(configuration),
            isProduction: true);

        Assert.Contains(failures, failure =>
            failure.Contains("loopback", StringComparison.OrdinalIgnoreCase));

        values["AdapterHost:ProductionAdmission:StatusEndpointExposure"] =
            "Disabled";
        configuration = Configuration(values);
        Assert.Empty(AdapterHostProductionAdmission.Validate(
            AdapterHostProductionAdmissionOptions.FromConfiguration(
                configuration),
            AdapterHostOptions.FromConfiguration(configuration),
            isProduction: true));
    }

    [Fact]
    public void Listener_rejects_a_route_base()
    {
        Dictionary<string, string?> values = ValidValues();
        values["AdapterHost:ListenUrl"] =
            "http://127.0.0.1:8088/adapter";

        Assert.Throws<InvalidOperationException>(() =>
            AdapterHostOptions.FromConfiguration(Configuration(values)));
    }

    [Fact]
    public async Task Startup_preflight_reads_token_and_disposes_material()
    {
        AdapterHostOptions options = AdapterHostOptions.FromConfiguration(
            Configuration(ValidValues()));
        TestMaterialProvider materials = new();
        AdapterHostStartupPreflight preflight = new(
            new TestTokenProvider("bfi_v1_test"),
            materials,
            options.CreateRuntimeIdentity());
        AdapterDescriptor descriptor = Descriptor();

        await preflight.ValidateAsync(descriptor, CancellationToken.None);

        Assert.NotNull(materials.LastMaterial);
        Assert.Throws<ObjectDisposedException>(() =>
            _ = materials.LastMaterial.Configuration);
    }

    [Fact]
    public async Task Startup_preflight_rejects_an_invalid_token_before_material_read()
    {
        AdapterHostOptions options = AdapterHostOptions.FromConfiguration(
            Configuration(ValidValues()));
        TestMaterialProvider materials = new();
        AdapterHostStartupPreflight preflight = new(
            new TestTokenProvider("not a token"),
            materials,
            options.CreateRuntimeIdentity());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            preflight.ValidateAsync(Descriptor(), CancellationToken.None));
        Assert.Null(materials.LastMaterial);
    }

    [Fact]
    public async Task Production_host_can_expose_health_without_mapping_status()
    {
        string directory = CreateDirectory();
        try
        {
            string configurationPath = Path.Combine(directory, "adapter.json");
            string tokenPath = Path.Combine(directory, "token.txt");
            await File.WriteAllTextAsync(configurationPath, "{}");
            await File.WriteAllTextAsync(tokenPath, "bfi_v1_test-token");
            await using ProductionAdapterHost application = new(
                configurationPath,
                tokenPath);
            using HttpClient client = application.CreateClient();

            HttpResponseMessage? ready = null;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                ready?.Dispose();
                ready = await client.GetAsync("/health/ready");
                if (ready.StatusCode == HttpStatusCode.OK)
                {
                    break;
                }

                await Task.Delay(50);
            }

            using (ready)
            {
                Assert.NotNull(ready);
                Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            }

            using HttpResponseMessage status = await client.GetAsync("/status");
            Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static AdapterDescriptor Descriptor() => new(
        "fake.http",
        protocolVersion: 1,
        configurationSchemaVersion: 1,
        [AdapterExecutionMode.Polling]);

    private static IConfiguration Configuration(
        Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> ApprovedValues()
    {
        Dictionary<string, string?> values = ValidValues();
        values["AdapterHost:CoordinationMode"] = "server-lease";
        values["AdapterHost:WorkerId"] =
            "30000000-0000-0000-0000-000000000001";
        values["AdapterHost:ServiceBaseAddress"] =
            "https://service.example.test/bunkfy";
        values["AdapterHost:AllowInsecureLoopback"] = "false";
        values["AdapterHost:ProductionAdmission:ApprovalState"] = "Approved";
        values["AdapterHost:ProductionAdmission:ApprovalReference"] =
            "ops/adapter-host/2026-08-05";
        values["AdapterHost:ProductionAdmission:DeploymentProfile"] =
            "SelfHosted";
        values["AdapterHost:ProductionAdmission:Runtime"] = "Process";
        values["AdapterHost:ProductionAdmission:SourceCommitSha"] =
            new string('1', 40);
        values["AdapterHost:ProductionAdmission:ApprovedAdapterType"] =
            "fake.http";
        values["AdapterHost:ProductionAdmission:ApprovedServiceBaseAddress"] =
            "https://service.example.test/bunkfy/";
        values["AdapterHost:ProductionAdmission:StatusEndpointExposure"] =
            "LoopbackOnly";
        return values;
    }

    private static Dictionary<string, string?> ValidValues() => new()
    {
        ["AdapterHost:AdapterType"] = "fake.http",
        ["AdapterHost:TenantId"] = "tenant-a",
        ["AdapterHost:PropertyId"] =
            "10000000-0000-0000-0000-000000000001",
        ["AdapterHost:ConnectionId"] =
            "20000000-0000-0000-0000-000000000001",
        ["AdapterHost:CoordinationMode"] = "local-file",
        ["AdapterHost:ServiceBaseAddress"] = "http://localhost:7001",
        ["AdapterHost:CheckpointFilePath"] = "checkpoint.json",
        ["AdapterHost:ConfigurationFilePath"] = "adapter.json",
        ["AdapterHost:ConfigurationContentType"] = "application/json",
        ["AdapterHost:IngressTokenEnvironmentVariable"] = "BUNKFY_TOKEN",
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
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"bunkfy-adapter-production-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class TestTokenProvider(string token)
        : IAdapterIngressTokenProvider
    {
        public ValueTask<string> GetTokenAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(token);
    }

    private sealed class TestMaterialProvider : IAdapterRuntimeMaterialProvider
    {
        public AdapterConfigurationMaterial? LastMaterial { get; private set; }

        public Task<AdapterConfigurationMaterial> ResolveAsync(
            AdapterRuntimeIdentity identity,
            int configurationSchemaVersion,
            CancellationToken cancellationToken)
        {
            this.LastMaterial = new AdapterConfigurationMaterial(
                configurationSchemaVersion,
                "application/json",
                "{}"u8);
            return Task.FromResult(this.LastMaterial);
        }
    }

    private sealed class ProductionAdapterHost(
        string configurationPath,
        string tokenPath)
        : WebApplicationFactory<AdapterHostAssemblyReference>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            Dictionary<string, string?> values = ApprovedValues();
            values["AdapterHost:ConfigurationFilePath"] = configurationPath;
            values["AdapterHost:IngressTokenEnvironmentVariable"] = string.Empty;
            values["AdapterHost:IngressTokenFilePath"] = tokenPath;
            values["AdapterHost:RunOnStart"] = "false";
            values["AdapterHost:ProductionAdmission:StatusEndpointExposure"] =
                "Disabled";
            foreach ((string key, string? value) in values)
            {
                builder.UseSetting(key, value);
            }
        }
    }
}
