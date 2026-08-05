namespace BunkFy.AdapterHost;

using Microsoft.Extensions.Logging;

internal static class AdapterHostProductionAdmission
{
    public static void ValidateOrThrow(
        AdapterHostProductionAdmissionOptions admission,
        AdapterHostOptions runtime,
        bool isProduction)
    {
        string[] failures = Validate(admission, runtime, isProduction);
        if (failures.Length > 0)
        {
            throw new InvalidOperationException(
                "AdapterHost Production admission failed: " +
                string.Join(" ", failures));
        }
    }

    public static string[] Validate(
        AdapterHostProductionAdmissionOptions admission,
        AdapterHostOptions runtime,
        bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(admission);
        ArgumentNullException.ThrowIfNull(runtime);
        if (!isProduction)
        {
            return [];
        }

        List<string> failures = [];
        if (admission.ApprovalState !=
            AdapterHostProductionApprovalState.Approved)
        {
            failures.Add(
                "AdapterHost:ProductionAdmission:ApprovalState must be Approved.");
        }

        if (!IsReference(admission.ApprovalReference))
        {
            failures.Add(
                "AdapterHost:ProductionAdmission:ApprovalReference must be a 3-128 character non-secret evidence reference.");
        }

        if (admission.DeploymentProfile is not (
                AdapterHostDeploymentProfile.SelfHosted or
                AdapterHostDeploymentProfile.Hosted))
        {
            failures.Add(
                "AdapterHost:ProductionAdmission:DeploymentProfile must be SelfHosted or Hosted.");
        }

        if (admission.Runtime is not (
                AdapterHostRuntimeKind.Process or
                AdapterHostRuntimeKind.Container))
        {
            failures.Add(
                "AdapterHost:ProductionAdmission:Runtime must be Process or Container.");
        }

        if (!IsCommitSha(admission.SourceCommitSha))
        {
            failures.Add(
                "AdapterHost:ProductionAdmission:SourceCommitSha must identify the exact lowercase 40-character Git commit.");
        }

        if (admission.Runtime == AdapterHostRuntimeKind.Container &&
            !IsImageDigest(admission.ContainerImageDigest))
        {
            failures.Add(
                "AdapterHost:ProductionAdmission:ContainerImageDigest must be an immutable lowercase sha256 OCI digest for a container.");
        }

        if (!string.Equals(
                admission.ApprovedAdapterType,
                runtime.AdapterType,
                StringComparison.Ordinal))
        {
            failures.Add(
                "AdapterHost:ProductionAdmission:ApprovedAdapterType must exactly match AdapterHost:AdapterType.");
        }

        if (!TryNormalizeServiceBaseAddress(
                admission.ApprovedServiceBaseAddress,
                out string? approvedBaseAddress) ||
            !string.Equals(
                approvedBaseAddress,
                NormalizeServiceBaseAddress(runtime.ServiceBaseAddress),
                StringComparison.Ordinal))
        {
            failures.Add(
                "AdapterHost:ProductionAdmission:ApprovedServiceBaseAddress must exactly match the HTTPS AdapterHost:ServiceBaseAddress.");
        }

        if (runtime.CoordinationMode != AdapterHostCoordinationMode.ServerLease)
        {
            failures.Add(
                "Production AdapterHost requires AdapterHost:CoordinationMode=server-lease.");
        }

        if (runtime.AllowInsecureLoopback)
        {
            failures.Add(
                "Production AdapterHost requires AdapterHost:AllowInsecureLoopback=false.");
        }

        if (admission.StatusEndpointExposure is not (
                AdapterHostStatusEndpointExposure.Disabled or
                AdapterHostStatusEndpointExposure.LoopbackOnly))
        {
            failures.Add(
                "AdapterHost:ProductionAdmission:StatusEndpointExposure must be Disabled or LoopbackOnly.");
        }
        else if (admission.StatusEndpointExposure ==
                 AdapterHostStatusEndpointExposure.LoopbackOnly &&
                 !new Uri(runtime.ListenUrl, UriKind.Absolute).IsLoopback)
        {
            failures.Add(
                "LoopbackOnly AdapterHost status requires a loopback AdapterHost:ListenUrl.");
        }

        return [.. failures];
    }

    public static void ReportApproved(
        ILogger logger,
        AdapterHostProductionAdmissionOptions admission,
        AdapterHostOptions runtime,
        bool isProduction)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (!isProduction)
        {
            return;
        }

        logger.LogInformation(
            "AdapterHost Production admission approved. Reference {ApprovalReference}; profile {DeploymentProfile}; runtime {Runtime}; source {SourceCommitSha}; image {ContainerImageDigest}; adapter {AdapterType}; service {ServiceBaseAddress}; coordination {CoordinationMode}; status {StatusEndpointExposure}.",
            admission.ApprovalReference,
            admission.DeploymentProfile,
            admission.Runtime,
            admission.SourceCommitSha,
            admission.ContainerImageDigest ?? "not-applicable",
            runtime.AdapterType,
            NormalizeServiceBaseAddress(runtime.ServiceBaseAddress),
            runtime.CoordinationMode,
            admission.StatusEndpointExposure);
    }

    private static bool TryNormalizeServiceBaseAddress(
        string? value,
        out string? normalized)
    {
        normalized = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri? address) ||
            !string.Equals(address.Scheme, Uri.UriSchemeHttps,
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(address.UserInfo) ||
            !string.IsNullOrEmpty(address.Query) ||
            !string.IsNullOrEmpty(address.Fragment))
        {
            return false;
        }

        normalized = NormalizeServiceBaseAddress(address);
        return true;
    }

    private static string NormalizeServiceBaseAddress(Uri address) =>
        address.AbsoluteUri.EndsWith('/')
            ? address.AbsoluteUri
            : address.AbsoluteUri + '/';

    private static bool IsReference(string? value)
    {
        if (value is not { Length: >= 3 and <= 128 })
        {
            return false;
        }

        return value.All(character =>
            char.IsAsciiLetterOrDigit(character) ||
            character is '.' or '_' or ':' or '/' or '-');
    }

    private static bool IsCommitSha(string? value) =>
        value is { Length: 40 } && IsLowerHex(value) && !IsAllZeros(value);

    private static bool IsImageDigest(string? value) =>
        value is { Length: 71 } &&
        value.StartsWith("sha256:", StringComparison.Ordinal) &&
        IsLowerHex(value.AsSpan(7)) &&
        !IsAllZeros(value.AsSpan(7));

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
