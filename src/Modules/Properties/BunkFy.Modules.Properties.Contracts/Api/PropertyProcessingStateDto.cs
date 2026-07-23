namespace BunkFy.Modules.Properties.Contracts;

public sealed record PropertyProcessingStateDto(
    Guid PropertyId,
    PropertyProcessingStatus ConfiguredStatus,
    PropertyProcessingEffectiveStatus EffectiveStatus,
    string ReasonCode,
    PropertyGovernancePolicyBindingDto? GovernancePolicy,
    long PropertyVersion,
    DateTimeOffset EvaluatedAtUtc);
