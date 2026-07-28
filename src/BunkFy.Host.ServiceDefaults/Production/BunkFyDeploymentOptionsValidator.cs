namespace BunkFy.Host.ServiceDefaults.Production;

using Gma.Framework.Api.Production;
using Gma.Framework.FileManagement.Minio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal sealed class BunkFyDeploymentOptionsValidator(
    IHostEnvironment environment,
    IConfiguration configuration,
    BunkFyDeploymentSurfaceRegistration surface)
    : IValidateOptions<BunkFyDeploymentOptions>
{
    public ValidateOptionsResult Validate(string? name, BunkFyDeploymentOptions options)
    {
        string[] failures = BunkFyDeploymentOptionsValidation.Validate(
            options,
            environment.IsProduction(),
            surface.Surface,
            configuration.GetSection(ProductionHttpOptions.SectionName)
                .Get<ProductionHttpOptions>() ?? new(),
            configuration.GetSection(ProductionDataProtectionOptions.SectionName)
                .Get<ProductionDataProtectionOptions>() ?? new(),
            configuration.GetSection(MinioFileStorageOptions.SectionName)
                .Get<MinioFileStorageOptions>() ?? new(),
            configuration.GetValue<bool>("FileManagement:Enabled"));

        return failures.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}

internal static class BunkFyDeploymentOptionsValidation
{
    public static string[] Validate(
        BunkFyDeploymentOptions options,
        bool isProduction,
        BunkFyDeploymentSurface surface,
        ProductionHttpOptions http,
        ProductionDataProtectionOptions dataProtection,
        MinioFileStorageOptions minio,
        bool fileManagementEnabled)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(dataProtection);
        ArgumentNullException.ThrowIfNull(minio);

        if (!isProduction)
        {
            return [];
        }

        List<string> failures = [];
        ValidateDeploymentIdentity(options, failures);
        ValidateHttpBoundary(options, surface, http, failures);

        if (surface == BunkFyDeploymentSurface.PublicApi)
        {
            ValidatePublicApi(options, http, dataProtection, failures);
        }
        else if (surface == BunkFyDeploymentSurface.AdminApi &&
                 !http.PrivateNetwork.Enabled)
        {
            failures.Add(
                "Production Admin API requires Http:PrivateNetwork:Enabled=true.");
        }

        if (fileManagementEnabled)
        {
            ValidateObjectStorage(options, minio, failures);
        }

        return [.. failures];
    }

    private static void ValidateDeploymentIdentity(
        BunkFyDeploymentOptions options,
        List<string> failures)
    {
        if (options.Profile is not (
                BunkFyDeploymentProfile.SelfHosted or
                BunkFyDeploymentProfile.Hosted))
        {
            failures.Add(
                "BunkFy:Deployment:Profile must be SelfHosted or Hosted in Production. Preview and Unspecified are not production profiles.");
        }

        if (options.ApiTopology is not (
                BunkFyApiTopology.SingleReplica or
                BunkFyApiTopology.MultiReplica))
        {
            failures.Add(
                "BunkFy:Deployment:ApiTopology must be SingleReplica or MultiReplica in Production.");
        }

        if (options.EdgeMode is not (
                BunkFyEdgeMode.DirectHttps or
                BunkFyEdgeMode.TrustedReverseProxy))
        {
            failures.Add(
                "BunkFy:Deployment:EdgeMode must be DirectHttps or TrustedReverseProxy in Production.");
        }

        if (options.Runtime is not (
                BunkFyRuntimeKind.Process or
                BunkFyRuntimeKind.Container))
        {
            failures.Add(
                "BunkFy:Deployment:Runtime must be Process or Container in Production.");
        }

        if (!IsCommitSha(options.SourceCommitSha))
        {
            failures.Add(
                "BunkFy:Deployment:SourceCommitSha must identify the exact lowercase 40-character Git commit in Production.");
        }

        if (options.Runtime == BunkFyRuntimeKind.Container &&
            !IsImageDigest(options.ContainerImageDigest))
        {
            failures.Add(
                "BunkFy:Deployment:ContainerImageDigest must be an immutable lowercase sha256 OCI digest for a Production container.");
        }

        if (options.DataProtectionKeyProtection is not (
                BunkFyKeyProtectionKind.EncryptedVolume or
                BunkFyKeyProtectionKind.Certificate or
                BunkFyKeyProtectionKind.Kms or
                BunkFyKeyProtectionKind.Hsm or
                BunkFyKeyProtectionKind.PlatformManaged))
        {
            failures.Add(
                "BunkFy:Deployment:DataProtectionKeyProtection must declare the deployment key-ring protection mechanism.");
        }
    }

    private static void ValidateHttpBoundary(
        BunkFyDeploymentOptions options,
        BunkFyDeploymentSurface surface,
        ProductionHttpOptions http,
        List<string> failures)
    {
        if (!http.HttpsRedirectionEnabled ||
            !http.HstsEnabled ||
            !http.SecurityHeadersEnabled)
        {
            failures.Add(
                "Production HTTP surfaces require HTTPS redirection, HSTS, and security headers.");
        }

        if (options.EdgeMode == BunkFyEdgeMode.DirectHttps &&
            http.ForwardedHeaders.Enabled)
        {
            failures.Add(
                "DirectHttps deployments must disable Http:ForwardedHeaders.");
        }

        if (options.EdgeMode == BunkFyEdgeMode.TrustedReverseProxy)
        {
            if (!http.ForwardedHeaders.Enabled ||
                http.ForwardedHeaders.AllowUnknownProxies ||
                (http.ForwardedHeaders.KnownProxies.Length == 0 &&
                 http.ForwardedHeaders.KnownNetworks.Length == 0))
            {
                failures.Add(
                    "TrustedReverseProxy deployments require forwarded headers, a known proxy IP or CIDR, and AllowUnknownProxies=false.");
            }
        }

        if (surface == BunkFyDeploymentSurface.PublicApi &&
            options.Profile == BunkFyDeploymentProfile.Hosted &&
            options.EdgeMode != BunkFyEdgeMode.TrustedReverseProxy)
        {
            failures.Add(
                "Hosted public API deployments require the TrustedReverseProxy edge mode.");
        }
    }

    private static void ValidatePublicApi(
        BunkFyDeploymentOptions options,
        ProductionHttpOptions http,
        ProductionDataProtectionOptions dataProtection,
        List<string> failures)
    {
        bool distributedRequired =
            options.Profile == BunkFyDeploymentProfile.Hosted ||
            options.ApiTopology == BunkFyApiTopology.MultiReplica;
        if (distributedRequired &&
            (!http.RateLimiting.Enabled ||
             http.RateLimiting.Mode != HttpRateLimitMode.Distributed))
        {
            failures.Add(
                "Hosted and multi-replica public APIs require enabled Distributed HTTP rate limiting.");
        }

        if (string.IsNullOrWhiteSpace(dataProtection.KeyRingPath))
        {
            failures.Add(
                "Production public API requires a persistent DataProtection:KeyRingPath.");
        }
    }

    private static void ValidateObjectStorage(
        BunkFyDeploymentOptions options,
        MinioFileStorageOptions minio,
        List<string> failures)
    {
        if (options.ObjectStorageCredentialProfile !=
            BunkFyStorageCredentialProfile.DedicatedServiceAccount)
        {
            failures.Add(
                "BunkFy:Deployment:ObjectStorageCredentialProfile must be DedicatedServiceAccount when file storage is enabled.");
        }

        if (options.Profile == BunkFyDeploymentProfile.Hosted &&
            (minio.AllowInsecureTransportInProduction ||
             minio.AllowBucketCreationInProduction ||
             minio.CreateBucketIfMissing))
        {
            failures.Add(
                "Hosted object storage cannot allow plaintext transport or application-owned bucket creation.");
        }
    }

    private static bool IsCommitSha(string? value) =>
        value is { Length: 40 } &&
        IsLowerHex(value) &&
        !IsAllZeros(value);

    private static bool IsImageDigest(string? value)
    {
        if (value is not { Length: 71 } ||
            !value.StartsWith("sha256:", StringComparison.Ordinal))
        {
            return false;
        }

        ReadOnlySpan<char> digest = value.AsSpan(7);
        return IsLowerHex(digest) && !IsAllZeros(digest);
    }

    private static bool IsLowerHex(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (!char.IsAsciiDigit(character) &&
                character is not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsAllZeros(ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            if (character != '0')
            {
                return false;
            }
        }

        return true;
    }
}

internal sealed record BunkFyDeploymentSurfaceRegistration(
    BunkFyDeploymentSurface Surface);
