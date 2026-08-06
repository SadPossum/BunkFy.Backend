namespace BunkFy.Host.ServiceDefaults.Tests.Production;

using BunkFy.Host.ServiceDefaults.Production;
using Gma.Framework.Api.Production;
using Gma.Framework.FileManagement.Minio;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class BunkFyDeploymentOptionsValidatorTests
{
    [Fact]
    public void Production_rejects_an_unspecified_deployment()
    {
        string[] failures = Validate(new BunkFyDeploymentOptions());

        Assert.Contains(failures, failure => failure.Contains("Profile", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("ApiTopology", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("EdgeMode", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("Runtime", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("ReleaseId", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("SourceCommitSha", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("PromotionEvidenceReference", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("RollbackEvidenceReference", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("AdmissionEvidenceReference", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("DataProtectionKeyProtection", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("ObjectStorageCredentialProfile", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_accepts_a_single_replica_self_hosted_public_api()
    {
        string[] failures = Validate(CreateValidOptions());

        Assert.Empty(failures);
    }

    [Fact]
    public void Production_rejects_undefined_deployment_enum_values()
    {
        BunkFyDeploymentOptions options = CreateValidOptions();
        options.ApiTopology = (BunkFyApiTopology)99;
        options.EdgeMode = (BunkFyEdgeMode)99;
        options.Runtime = (BunkFyRuntimeKind)99;
        options.DataProtectionKeyProtection = (BunkFyKeyProtectionKind)99;

        string[] failures = Validate(options);

        Assert.Contains(failures, failure => failure.Contains("ApiTopology", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("EdgeMode", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("Runtime", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("DataProtectionKeyProtection", StringComparison.Ordinal));
    }

    [Fact]
    public void Hosted_public_api_requires_a_trusted_proxy_and_distributed_rate_limiting()
    {
        BunkFyDeploymentOptions options = CreateValidOptions();
        options.Profile = BunkFyDeploymentProfile.Hosted;

        string[] failures = Validate(options);

        Assert.Contains(
            failures,
            failure => failure.Contains("TrustedReverseProxy", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("Distributed HTTP rate limiting", StringComparison.Ordinal));

        options.EdgeMode = BunkFyEdgeMode.TrustedReverseProxy;
        ProductionHttpOptions http = CreateValidHttp();
        http.ForwardedHeaders.Enabled = true;
        http.ForwardedHeaders.KnownNetworks = ["10.20.0.0/16"];
        http.RateLimiting.Mode = HttpRateLimitMode.Distributed;

        Assert.Empty(Validate(options, http: http));
    }

    [Fact]
    public void Container_runtime_requires_an_immutable_image_digest()
    {
        BunkFyDeploymentOptions options = CreateValidOptions();
        options.Runtime = BunkFyRuntimeKind.Container;

        string[] failures = Validate(options);

        Assert.Contains(
            failures,
            failure => failure.Contains("ContainerImageDigest", StringComparison.Ordinal));

        options.ContainerImageDigest = $"sha256:{new string('b', 64)}";

        Assert.Empty(Validate(options));
    }

    [Fact]
    public void Production_rejects_placeholder_release_identity()
    {
        BunkFyDeploymentOptions options = CreateValidOptions();
        options.ReleaseId = "bad release id";
        options.SourceCommitSha = new string('0', 40);
        options.Runtime = BunkFyRuntimeKind.Container;
        options.ContainerImageDigest = $"sha256:{new string('0', 64)}";
        options.PromotionEvidenceReference = "?";
        options.RollbackEvidenceReference = null;
        options.AdmissionEvidenceReference = "admission:not-a-closed-bundle";

        string[] failures = Validate(options);

        Assert.Contains(
            failures,
            failure => failure.Contains("ReleaseId", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("SourceCommitSha", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("ContainerImageDigest", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("PromotionEvidenceReference", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("RollbackEvidenceReference", StringComparison.Ordinal));
        Assert.Contains(
            failures,
            failure => failure.Contains("AdmissionEvidenceReference", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_rejects_empty_or_non_lowercase_admission_identity()
    {
        string[] invalidReferences =
        [
            $"admission:{new string('0', 32)}",
            $"admission:{new string('A', 32)}"
        ];

        foreach (string reference in invalidReferences)
        {
            BunkFyDeploymentOptions options = CreateValidOptions();
            options.AdmissionEvidenceReference = reference;

            string[] failures = Validate(options);

            Assert.Contains(
                failures,
                failure => failure.Contains(
                    "AdmissionEvidenceReference",
                    StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Admin_api_requires_a_private_network_boundary()
    {
        ProductionHttpOptions http = CreateValidHttp();
        http.PrivateNetwork.Enabled = false;

        string[] failures = Validate(
            CreateValidOptions(),
            surface: BunkFyDeploymentSurface.AdminApi,
            http: http);

        Assert.Contains(
            failures,
            failure => failure.Contains("PrivateNetwork", StringComparison.Ordinal));
    }

    [Fact]
    public void Worker_requires_release_identity_without_http_only_declarations()
    {
        BunkFyDeploymentOptions options = new()
        {
            Profile = BunkFyDeploymentProfile.SelfHosted,
            Runtime = BunkFyRuntimeKind.Process,
            ReleaseId = "release-worker-001",
            SourceCommitSha = new string('a', 40),
            PromotionEvidenceReference = "promotion-worker-001",
            RollbackEvidenceReference = "recovery-worker-001",
            AdmissionEvidenceReference = $"admission:{new string('b', 32)}"
        };

        string[] failures = Validate(
            options,
            surface: BunkFyDeploymentSurface.Worker,
            http: new ProductionHttpOptions(),
            dataProtection: new ProductionDataProtectionOptions(),
            fileManagementEnabled: false);

        Assert.Empty(failures);
    }

    [Fact]
    public void Hosted_storage_rejects_insecure_transport_and_application_owned_bucket_creation()
    {
        BunkFyDeploymentOptions options = CreateValidOptions();
        options.Profile = BunkFyDeploymentProfile.Hosted;
        options.EdgeMode = BunkFyEdgeMode.TrustedReverseProxy;
        ProductionHttpOptions http = CreateValidHttp();
        http.ForwardedHeaders.Enabled = true;
        http.ForwardedHeaders.KnownProxies = ["10.20.0.5"];
        http.RateLimiting.Mode = HttpRateLimitMode.Distributed;
        MinioFileStorageOptions minio = CreateValidMinio();
        minio.AllowInsecureTransportInProduction = true;
        minio.CreateBucketIfMissing = true;

        string[] failures = Validate(options, http: http, minio: minio);

        Assert.Contains(
            failures,
            failure => failure.Contains("object storage", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Non_production_does_not_require_a_deployment_declaration()
    {
        string[] failures = Validate(
            new BunkFyDeploymentOptions(),
            isProduction: false);

        Assert.Empty(failures);
    }

    [Fact]
    public void Production_composition_rejects_a_preview_profile()
    {
        HostApplicationBuilder builder = CreateProductionBuilder();
        builder.Configuration["BunkFy:Deployment:Profile"] = "Preview";

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            builder.AddBunkFyProductionDeployment(BunkFyDeploymentSurface.PublicApi));

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("Profile", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_composition_accepts_a_complete_public_declaration()
    {
        HostApplicationBuilder builder = CreateProductionBuilder();

        builder.AddBunkFyProductionDeployment(BunkFyDeploymentSurface.PublicApi);

        using IHost host = builder.Build();
        BunkFyDeploymentOptions options =
            host.Services.GetRequiredService<IOptions<BunkFyDeploymentOptions>>().Value;
        Assert.Equal(BunkFyDeploymentProfile.SelfHosted, options.Profile);
        Assert.Equal("release-api-001", options.ReleaseId);
        Assert.Equal("promotion-api-001", options.PromotionEvidenceReference);
        Assert.Equal("recovery-api-001", options.RollbackEvidenceReference);
        Assert.Equal(
            $"admission:{new string('b', 32)}",
            options.AdmissionEvidenceReference);
        Assert.Equal(BunkFyApiTopology.SingleReplica, options.ApiTopology);
        Assert.Single(
            host.Services.GetServices<IHostedService>()
                .OfType<BunkFyProductionDeploymentReporter>());
    }

    [Fact]
    public void Non_production_composition_does_not_register_the_admission_reporter()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Development
            });

        builder.AddBunkFyProductionDeployment(BunkFyDeploymentSurface.PublicApi);

        using IHost host = builder.Build();
        Assert.Empty(
            host.Services.GetServices<IHostedService>()
                .OfType<BunkFyProductionDeploymentReporter>());
    }

    private static string[] Validate(
        BunkFyDeploymentOptions options,
        bool isProduction = true,
        BunkFyDeploymentSurface surface = BunkFyDeploymentSurface.PublicApi,
        ProductionHttpOptions? http = null,
        ProductionDataProtectionOptions? dataProtection = null,
        MinioFileStorageOptions? minio = null,
        bool fileManagementEnabled = true) =>
        BunkFyDeploymentOptionsValidation.Validate(
            options,
            isProduction,
            surface,
            http ?? CreateValidHttp(),
            dataProtection ?? new ProductionDataProtectionOptions
            {
                KeyRingPath = "data/data-protection"
            },
            minio ?? CreateValidMinio(),
            fileManagementEnabled);

    private static BunkFyDeploymentOptions CreateValidOptions() => new()
    {
        Profile = BunkFyDeploymentProfile.SelfHosted,
        ApiTopology = BunkFyApiTopology.SingleReplica,
        EdgeMode = BunkFyEdgeMode.DirectHttps,
        Runtime = BunkFyRuntimeKind.Process,
        ReleaseId = "release-api-001",
        SourceCommitSha = new string('a', 40),
        PromotionEvidenceReference = "promotion-api-001",
        RollbackEvidenceReference = "recovery-api-001",
        AdmissionEvidenceReference = $"admission:{new string('b', 32)}",
        DataProtectionKeyProtection = BunkFyKeyProtectionKind.EncryptedVolume,
        ObjectStorageCredentialProfile =
            BunkFyStorageCredentialProfile.DedicatedServiceAccount
    };

    private static ProductionHttpOptions CreateValidHttp() => new()
    {
        HttpsRedirectionEnabled = true,
        HstsEnabled = true,
        SecurityHeadersEnabled = true,
        PrivateNetwork = new PrivateNetworkSettings
        {
            Enabled = true
        },
        RateLimiting = new RateLimitingSettings
        {
            Enabled = true,
            Mode = HttpRateLimitMode.InProcess
        }
    };

    private static MinioFileStorageOptions CreateValidMinio() => new()
    {
        Endpoint = "https://storage.example.test",
        UseSsl = true,
        CreateBucketIfMissing = false
    };

    private static HostApplicationBuilder CreateProductionBuilder()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Production
            });
        builder.Configuration["BunkFy:Deployment:Profile"] = "SelfHosted";
        builder.Configuration["BunkFy:Deployment:ApiTopology"] = "SingleReplica";
        builder.Configuration["BunkFy:Deployment:EdgeMode"] = "DirectHttps";
        builder.Configuration["BunkFy:Deployment:Runtime"] = "Process";
        builder.Configuration["BunkFy:Deployment:ReleaseId"] = "release-api-001";
        builder.Configuration["BunkFy:Deployment:SourceCommitSha"] = new string('a', 40);
        builder.Configuration["BunkFy:Deployment:PromotionEvidenceReference"] =
            "promotion-api-001";
        builder.Configuration["BunkFy:Deployment:RollbackEvidenceReference"] =
            "recovery-api-001";
        builder.Configuration["BunkFy:Deployment:AdmissionEvidenceReference"] =
            $"admission:{new string('b', 32)}";
        builder.Configuration["BunkFy:Deployment:DataProtectionKeyProtection"] =
            "EncryptedVolume";
        builder.Configuration["DataProtection:KeyRingPath"] = "data/data-protection";
        builder.Configuration["FileManagement:Enabled"] = "false";

        return builder;
    }
}
