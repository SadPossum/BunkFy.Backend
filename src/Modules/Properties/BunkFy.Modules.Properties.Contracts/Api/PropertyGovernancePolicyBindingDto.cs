namespace BunkFy.Modules.Properties.Contracts;

public sealed record PropertyGovernancePolicyBindingDto(
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string DataRegionId,
    string TransferProfileId,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    DateTimeOffset PolicyEffectiveAtUtc,
    DateTimeOffset PolicyExpiresAtUtc,
    DateTimeOffset ActivatedAtUtc,
    IReadOnlyCollection<PropertyGovernanceAcknowledgementDto> Acknowledgements);

public sealed record PropertyGovernanceAcknowledgementDto(
    string AcknowledgementId,
    int AcknowledgementVersion);
