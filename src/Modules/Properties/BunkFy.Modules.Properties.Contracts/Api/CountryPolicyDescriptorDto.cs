namespace BunkFy.Modules.Properties.Contracts;

public sealed record CountryPolicyListResponse(
    IReadOnlyCollection<CountryPolicyDescriptorDto> Items);

public sealed record CountryPolicyDescriptorDto(
    string PolicyId,
    int PolicyVersion,
    string OperatingCountryCode,
    string LaunchStatus,
    string ApprovalState,
    DateTimeOffset EffectiveAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string ContentSha256,
    IReadOnlyCollection<string> AccommodationTypes,
    IReadOnlyCollection<string> PermittedDataRegions,
    IReadOnlyCollection<string> PermittedTransferProfiles,
    IReadOnlyCollection<CountryPolicyRetentionDescriptorDto> RetentionPolicies,
    IReadOnlyCollection<PropertyGovernanceAcknowledgementDto> RequiredAcknowledgements);

public sealed record CountryPolicyRetentionDescriptorDto(
    string RetentionPolicyId,
    int RetentionPolicyVersion);
