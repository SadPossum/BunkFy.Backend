namespace BunkFy.DataGovernance;

using System.Globalization;

public sealed class CountryPolicyRegistry
{
    public const int MaximumPolicyArtifacts = 128;
    public const int MaximumAllowlistEntries = 128;
    public const int MaximumAcceptedAcknowledgements = 64;

    private readonly IReadOnlyDictionary<CountryPolicyIdentity, RegisteredPolicy> policies;

    private CountryPolicyRegistry(
        CountryPolicyRuntimeMode runtimeMode,
        IReadOnlyDictionary<CountryPolicyIdentity, RegisteredPolicy> policies)
    {
        this.RuntimeMode = runtimeMode;
        this.policies = policies;
    }

    public CountryPolicyRuntimeMode RuntimeMode { get; }

    public static CountryPolicyRegistry Create(
        IEnumerable<CountryPolicyPackArtifact> artifacts,
        IEnumerable<CountryPolicyAllowlistEntry> allowlist,
        CountryPolicyRuntimeMode runtimeMode)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(allowlist);
        if (!Enum.IsDefined(runtimeMode))
        {
            throw new ArgumentOutOfRangeException(nameof(runtimeMode));
        }

        Dictionary<CountryPolicyIdentity, CountryPolicyPackArtifact> artifactIndex = [];
        List<string> errors = [];
        int artifactCount = 0;
        foreach (CountryPolicyPackArtifact artifact in artifacts)
        {
            artifactCount++;
            if (artifactCount > MaximumPolicyArtifacts)
            {
                errors.Add($"A country-policy registry cannot contain more than {MaximumPolicyArtifacts} artifacts.");
                break;
            }

            if (artifact?.Document is null)
            {
                errors.Add("Country-policy artifacts cannot contain null documents.");
                continue;
            }

            CountryPolicyPackArtifact frozenArtifact = Freeze(artifact);
            IReadOnlyList<string> artifactErrors = CountryPolicyPackValidator.Validate(frozenArtifact.Document);
            if (artifactErrors.Count > 0)
            {
                errors.AddRange(artifactErrors);
                continue;
            }

            if (!IsSha256(artifact.ContentSha256))
            {
                errors.Add($"Policy '{artifact.Document.PolicyId}:{artifact.Document.PolicyVersion}' has an invalid content digest.");
                continue;
            }

            CountryPolicyIdentity identity = new(
                frozenArtifact.Document.PolicyId,
                frozenArtifact.Document.PolicyVersion);
            if (!artifactIndex.TryAdd(identity, frozenArtifact))
            {
                errors.Add($"Duplicate country-policy artifact '{identity}'.");
            }
        }

        Dictionary<CountryPolicyIdentity, RegisteredPolicy> registered = [];
        int allowlistCount = 0;
        foreach (CountryPolicyAllowlistEntry entry in allowlist)
        {
            allowlistCount++;
            if (allowlistCount > MaximumAllowlistEntries)
            {
                errors.Add($"A country-policy registry cannot contain more than {MaximumAllowlistEntries} allowlist entries.");
                break;
            }

            if (entry is null)
            {
                errors.Add("Country-policy allowlist entries cannot be null.");
                continue;
            }

            CountryPolicyIdentity identity = new(entry.PolicyId, entry.PolicyVersion);
            ValidateAllowlistEntry(entry, runtimeMode, errors);
            if (!artifactIndex.TryGetValue(identity, out CountryPolicyPackArtifact? artifact))
            {
                errors.Add($"Allowlist entry '{identity}' does not have a matching policy artifact.");
                continue;
            }

            if (!string.Equals(entry.OperatingCountryCode, artifact.Document.OperatingCountryCode, StringComparison.Ordinal))
            {
                errors.Add($"Allowlist entry '{identity}' does not match the artifact operating country.");
            }

            if (!string.Equals(entry.ContentSha256, artifact.ContentSha256, StringComparison.Ordinal))
            {
                errors.Add($"Allowlist entry '{identity}' does not match the artifact content digest.");
            }

            if (runtimeMode == CountryPolicyRuntimeMode.Production &&
                entry.LaunchStatus == CountryLaunchStatus.Approved &&
                artifact.Document.ApprovalState != CountryPolicyApprovalState.Approved)
            {
                errors.Add($"Production policy '{identity}' must contain approved policy metadata.");
            }

            if (!registered.TryAdd(identity, new(artifact, entry)))
            {
                errors.Add($"Duplicate country-policy allowlist entry '{identity}'.");
            }
        }

        if (errors.Count > 0)
        {
            throw new CountryPolicyRegistryValidationException(errors);
        }

        return new(runtimeMode, registered);
    }

    public IReadOnlyList<CountryPolicyDescriptor> ListPolicies() =>
        this.policies.Values
            .Select(policy => new CountryPolicyDescriptor(
                policy.Artifact.Document.PolicyId,
                policy.Artifact.Document.PolicyVersion,
                policy.Artifact.Document.OperatingCountryCode,
                policy.Entry.LaunchStatus,
                policy.Artifact.Document.ApprovalState,
                policy.Artifact.Document.EffectiveAtUtc,
                policy.Artifact.Document.ExpiresAtUtc,
                policy.Artifact.ContentSha256,
                [.. policy.Artifact.Document.AccommodationTypes],
                [.. policy.Artifact.Document.PermittedDataRegions],
                [.. policy.Artifact.Document.PermittedTransferProfiles],
                [.. policy.Artifact.Document.RetentionRules.Select(rule =>
                    new CountryPolicyRetentionDescriptor(rule.RetentionPolicyId, rule.RetentionPolicyVersion))],
                [.. policy.Artifact.Document.RequiredAcknowledgements.Select(requirement =>
                    new CountryPolicyAcknowledgement(requirement.AcknowledgementId, requirement.AcknowledgementVersion))]))
            .OrderBy(policy => policy.OperatingCountryCode, StringComparer.Ordinal)
            .ThenBy(policy => policy.PolicyId, StringComparer.Ordinal)
            .ThenBy(policy => policy.PolicyVersion)
            .ToArray();

    public CountryPolicyDecision EvaluateActivation(CountryPolicyActivationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CountryPolicyBinding provisional = new(
            request.OperatingCountryCode,
            request.PolicyId,
            request.PolicyVersion,
            request.DataRegionId,
            request.TransferProfileId,
            request.RetentionPolicyId,
            request.RetentionPolicyVersion,
            string.Empty,
            request.AcceptedAcknowledgements ?? []);

        return this.Evaluate(
            provisional,
            request.AccommodationType,
            request.PurposeCode,
            CountryPolicySurface.PropertyActivation,
            request.SourceProvenance,
            request.ObservedAtUtc,
            requireBindingDigest: false);
    }

    public CountryPolicyDecision EvaluateOperation(CountryPolicyOperationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Binding is null)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.MissingBinding);
        }

        return this.Evaluate(
            request.Binding,
            request.AccommodationType,
            request.PurposeCode,
            request.Surface,
            request.SourceProvenance,
            request.ObservedAtUtc,
            requireBindingDigest: true);
    }

    private CountryPolicyDecision Evaluate(
        CountryPolicyBinding binding,
        string accommodationType,
        string purposeCode,
        CountryPolicySurface surface,
        string sourceProvenance,
        DateTimeOffset observedAtUtc,
        bool requireBindingDigest)
    {
        if (!IsCountryCode(binding.OperatingCountryCode) ||
            !IsKey(binding.PolicyId) || binding.PolicyVersion <= 0 ||
            !IsKey(binding.DataRegionId) || !IsKey(binding.TransferProfileId) ||
            !IsKey(binding.RetentionPolicyId) || binding.RetentionPolicyVersion <= 0 ||
            !IsKey(accommodationType) || !IsKey(purposeCode) || !IsKey(sourceProvenance) ||
            !Enum.IsDefined(surface) || surface == CountryPolicySurface.Unknown || observedAtUtc == default)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.InvalidRequest);
        }

        CountryPolicyIdentity identity = new(binding.PolicyId, binding.PolicyVersion);
        if (!this.policies.TryGetValue(identity, out RegisteredPolicy? registered))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.UnknownPolicy);
        }

        CountryPolicyPackDocument pack = registered.Artifact.Document;
        if (!string.Equals(pack.OperatingCountryCode, binding.OperatingCountryCode, StringComparison.Ordinal))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.CountryMismatch);
        }

        if (registered.Entry.LaunchStatus == CountryLaunchStatus.Disabled)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.CountryDisabled);
        }

        if (this.RuntimeMode == CountryPolicyRuntimeMode.Production &&
            registered.Entry.LaunchStatus != CountryLaunchStatus.Approved)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.CountryNotApproved);
        }

        if (this.RuntimeMode == CountryPolicyRuntimeMode.Production &&
            pack.ApprovalState != CountryPolicyApprovalState.Approved)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.PolicyNotApproved);
        }

        if (requireBindingDigest &&
            !string.Equals(binding.ContentSha256, registered.Artifact.ContentSha256, StringComparison.Ordinal))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.ContentDigestMismatch);
        }

        if (observedAtUtc < pack.EffectiveAtUtc)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.PolicyNotEffective);
        }

        if (observedAtUtc >= pack.ExpiresAtUtc)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.PolicyExpired);
        }

        if (!pack.AccommodationTypes.Contains(accommodationType, StringComparer.Ordinal))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.AccommodationTypeUnsupported);
        }

        if (!pack.PermittedDataRegions.Contains(binding.DataRegionId, StringComparer.Ordinal))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.DataRegionNotPermitted);
        }

        if (!pack.PermittedTransferProfiles.Contains(binding.TransferProfileId, StringComparer.Ordinal))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.TransferProfileNotPermitted);
        }

        if (!pack.RetentionRules.Any(rule =>
                string.Equals(rule.RetentionPolicyId, binding.RetentionPolicyId, StringComparison.Ordinal) &&
                rule.RetentionPolicyVersion == binding.RetentionPolicyVersion))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.RetentionPolicyNotPermitted);
        }

        if (!TrySnapshotAcknowledgements(binding.AcceptedAcknowledgements, out CountryPolicyAcknowledgement[] accepted))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.InvalidRequest);
        }

        if (pack.RequiredAcknowledgements.Any(required =>
                !accepted.Any(candidate =>
                    string.Equals(candidate.AcknowledgementId, required.AcknowledgementId, StringComparison.Ordinal) &&
                    candidate.AcknowledgementVersion == required.AcknowledgementVersion)))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.RequiredAcknowledgementMissing);
        }

        if (accepted.Any(candidate =>
                !pack.RequiredAcknowledgements.Any(required =>
                    string.Equals(candidate.AcknowledgementId, required.AcknowledgementId, StringComparison.Ordinal) &&
                    candidate.AcknowledgementVersion == required.AcknowledgementVersion)))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.InvalidRequest);
        }

        CountryPolicyPurposeRule? purpose = pack.PurposeRules.FirstOrDefault(rule =>
            string.Equals(rule.PurposeCode, purposeCode, StringComparison.Ordinal));
        if (purpose is null)
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.PurposeUnsupported);
        }

        if (!purpose.AllowedSurfaces.Contains(surface))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.SurfaceUnsupported);
        }

        if (!purpose.AllowedSourceProvenance.Contains(sourceProvenance, StringComparer.Ordinal))
        {
            return CountryPolicyDecision.Deny(CountryPolicyDecisionReason.SourceProvenanceUnsupported);
        }

        CountryPolicyEvidence evidence = new(
            pack.OperatingCountryCode,
            pack.PolicyId,
            pack.PolicyVersion,
            binding.DataRegionId,
            binding.TransferProfileId,
            binding.RetentionPolicyId,
            binding.RetentionPolicyVersion,
            registered.Artifact.ContentSha256,
            purposeCode,
            surface,
            sourceProvenance,
            pack.ApprovalState,
            pack.EffectiveAtUtc,
            pack.ExpiresAtUtc,
            observedAtUtc,
            Array.AsReadOnly(accepted.OrderBy(item => item.AcknowledgementId, StringComparer.Ordinal)
                .ThenBy(item => item.AcknowledgementVersion)
                .ToArray()));
        return CountryPolicyDecision.Allow(evidence);
    }

    private static void ValidateAllowlistEntry(
        CountryPolicyAllowlistEntry entry,
        CountryPolicyRuntimeMode runtimeMode,
        List<string> errors)
    {
        if (!IsCountryCode(entry.OperatingCountryCode))
        {
            errors.Add("Allowlist OperatingCountryCode must be a recognized uppercase ISO 3166-1 alpha-2 code.");
        }

        if (!IsKey(entry.PolicyId))
        {
            errors.Add("Allowlist PolicyId must be a lowercase ASCII policy key.");
        }

        if (entry.PolicyVersion <= 0)
        {
            errors.Add("Allowlist PolicyVersion must be greater than zero.");
        }

        if (!IsSha256(entry.ContentSha256))
        {
            errors.Add("Allowlist ContentSha256 must be a lowercase SHA-256 digest.");
        }

        if (!Enum.IsDefined(entry.LaunchStatus) || entry.LaunchStatus == CountryLaunchStatus.Unknown)
        {
            errors.Add("Allowlist LaunchStatus must be a known non-zero value.");
        }

        if (runtimeMode == CountryPolicyRuntimeMode.Production &&
            entry.LaunchStatus == CountryLaunchStatus.Engineering)
        {
            errors.Add("Engineering country launches cannot be configured in production mode.");
        }
    }

    private static bool TrySnapshotAcknowledgements(
        IReadOnlyCollection<CountryPolicyAcknowledgement>? source,
        out CountryPolicyAcknowledgement[] acknowledgements)
    {
        if (source is null)
        {
            acknowledgements = [];
            return true;
        }

        if (source.Count is < 0 or > MaximumAcceptedAcknowledgements)
        {
            acknowledgements = [];
            return false;
        }

        List<CountryPolicyAcknowledgement> snapshot = new(source.Count);
        HashSet<string> identities = new(StringComparer.Ordinal);
        foreach (CountryPolicyAcknowledgement? acknowledgement in source)
        {
            if (snapshot.Count >= MaximumAcceptedAcknowledgements || acknowledgement is null ||
                !IsKey(acknowledgement.AcknowledgementId) || acknowledgement.AcknowledgementVersion <= 0 ||
                !identities.Add($"{acknowledgement.AcknowledgementId}|{acknowledgement.AcknowledgementVersion}"))
            {
                acknowledgements = [];
                return false;
            }

            snapshot.Add(new(
                acknowledgement.AcknowledgementId,
                acknowledgement.AcknowledgementVersion));
        }

        acknowledgements = snapshot.ToArray();
        return true;
    }

    private static CountryPolicyPackArtifact Freeze(CountryPolicyPackArtifact artifact) =>
        new(CloneDocument(artifact.Document), artifact.ContentSha256);

    private static CountryPolicyPackDocument CloneDocument(CountryPolicyPackDocument pack) => pack with
    {
        AccommodationTypes = pack.AccommodationTypes?.ToArray()!,
        GuestCategories = pack.GuestCategories?.ToArray()!,
        FieldRules = pack.FieldRules?.Select(rule => rule is null
            ? null!
            : rule with { PurposeCodes = rule.PurposeCodes?.ToArray()! }).ToArray()!,
        PurposeRules = pack.PurposeRules?.Select(rule => rule is null
            ? null!
            : rule with
            {
                LegalRuleReferenceKeys = rule.LegalRuleReferenceKeys?.ToArray()!,
                AllowedSurfaces = rule.AllowedSurfaces?.ToArray()!,
                AllowedSourceProvenance = rule.AllowedSourceProvenance?.ToArray()!
            }).ToArray()!,
        RetentionRules = pack.RetentionRules?.Select(rule => rule is null ? null! : rule with { }).ToArray()!,
        RightsRule = pack.RightsRule is null ? null! : pack.RightsRule with { },
        Restrictions = pack.Restrictions is null ? null! : pack.Restrictions with { },
        PermittedDataRegions = pack.PermittedDataRegions?.ToArray()!,
        PermittedTransferProfiles = pack.PermittedTransferProfiles?.ToArray()!,
        RequiredAcknowledgements = pack.RequiredAcknowledgements?
            .Select(requirement => requirement is null ? null! : requirement with { })
            .ToArray()!,
        Approval = pack.Approval is null
            ? null!
            : pack.Approval with
            {
                Sources = pack.Approval.Sources?
                    .Select(source => source is null ? null! : source with { })
                    .ToArray()!
            }
    };

    private static bool IsCountryCode(string? value) => Iso3166Alpha2CountryCodes.Contains(value);

    private static bool IsKey(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        value[0] is >= 'a' and <= 'z' &&
        value.All(character =>
            character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '_');

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private sealed record RegisteredPolicy(
        CountryPolicyPackArtifact Artifact,
        CountryPolicyAllowlistEntry Entry);
}

public sealed record CountryPolicyAllowlistEntry(
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string ContentSha256,
    CountryLaunchStatus LaunchStatus);

public sealed record CountryPolicyActivationRequest(
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    IReadOnlyCollection<CountryPolicyAcknowledgement> AcceptedAcknowledgements,
    string AccommodationType,
    string PurposeCode,
    string SourceProvenance,
    DateTimeOffset ObservedAtUtc);

public sealed record CountryPolicyOperationRequest(
    CountryPolicyBinding? Binding,
    string AccommodationType,
    string PurposeCode,
    CountryPolicySurface Surface,
    string SourceProvenance,
    DateTimeOffset ObservedAtUtc);

public sealed record CountryPolicyBinding(
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    IReadOnlyCollection<CountryPolicyAcknowledgement> AcceptedAcknowledgements);

public sealed record CountryPolicyAcknowledgement(
    string AcknowledgementId,
    int AcknowledgementVersion);

public sealed record CountryPolicyEvidence(
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    string PurposeCode,
    CountryPolicySurface Surface,
    string SourceProvenance,
    CountryPolicyApprovalState ApprovalState,
    DateTimeOffset EffectiveAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset EvaluatedAtUtc,
    IReadOnlyCollection<CountryPolicyAcknowledgement> AcceptedAcknowledgements)
{
    public CountryPolicyBinding ToBinding() =>
        new(
            this.OperatingCountryCode,
            this.PolicyId,
            this.PolicyVersion,
            this.DataRegionId,
            this.TransferProfileId,
            this.RetentionPolicyId,
            this.RetentionPolicyVersion,
            this.ContentSha256,
            this.AcceptedAcknowledgements.ToArray());
}

public sealed record CountryPolicyDecision
{
    private CountryPolicyDecision(
        bool isAllowed,
        CountryPolicyDecisionReason reason,
        CountryPolicyEvidence? evidence)
    {
        this.IsAllowed = isAllowed;
        this.Reason = reason;
        this.Evidence = evidence;
    }

    public bool IsAllowed { get; }
    public CountryPolicyDecisionReason Reason { get; }
    public CountryPolicyEvidence? Evidence { get; }

    public static CountryPolicyDecision Allow(CountryPolicyEvidence evidence) =>
        new(true, CountryPolicyDecisionReason.Allowed, evidence ?? throw new ArgumentNullException(nameof(evidence)));

    public static CountryPolicyDecision Deny(CountryPolicyDecisionReason reason)
    {
        if (!Enum.IsDefined(reason) || reason is CountryPolicyDecisionReason.Unknown or CountryPolicyDecisionReason.Allowed)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        return new(false, reason, null);
    }
}

public enum CountryPolicyDecisionReason
{
    Unknown = 0,
    Allowed = 1,
    MissingBinding = 2,
    InvalidRequest = 3,
    UnknownPolicy = 4,
    CountryMismatch = 5,
    CountryDisabled = 6,
    CountryNotApproved = 7,
    PolicyNotApproved = 8,
    ContentDigestMismatch = 9,
    PolicyNotEffective = 10,
    PolicyExpired = 11,
    AccommodationTypeUnsupported = 12,
    DataRegionNotPermitted = 13,
    TransferProfileNotPermitted = 14,
    RetentionPolicyNotPermitted = 15,
    RequiredAcknowledgementMissing = 16,
    PurposeUnsupported = 17,
    SurfaceUnsupported = 18,
    SourceProvenanceUnsupported = 19
}

public sealed record CountryPolicyDescriptor(
    string PolicyId,
    int PolicyVersion,
    string OperatingCountryCode,
    CountryLaunchStatus LaunchStatus,
    CountryPolicyApprovalState ApprovalState,
    DateTimeOffset EffectiveAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string ContentSha256,
    IReadOnlyCollection<string> AccommodationTypes,
    IReadOnlyCollection<string> PermittedDataRegions,
    IReadOnlyCollection<string> PermittedTransferProfiles,
    IReadOnlyCollection<CountryPolicyRetentionDescriptor> RetentionPolicies,
    IReadOnlyCollection<CountryPolicyAcknowledgement> RequiredAcknowledgements);

public sealed record CountryPolicyRetentionDescriptor(string RetentionPolicyId, int RetentionPolicyVersion);

public readonly record struct CountryPolicyIdentity(string PolicyId, int PolicyVersion)
{
    public override string ToString() => $"{this.PolicyId}:{this.PolicyVersion.ToString(CultureInfo.InvariantCulture)}";
}

public sealed class CountryPolicyRegistryValidationException(IReadOnlyList<string> errors)
    : Exception("The country-policy registry is invalid:" + Environment.NewLine + string.Join(Environment.NewLine, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors.ToArray();
}
