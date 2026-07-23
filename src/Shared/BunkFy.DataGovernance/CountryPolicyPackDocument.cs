namespace BunkFy.DataGovernance;

using System.Text.Json.Serialization;

public sealed record CountryPolicyPackDocument
{
    [JsonRequired]
    public int SchemaVersion { get; init; }

    [JsonRequired]
    public required string PolicyId { get; init; }

    [JsonRequired]
    public int PolicyVersion { get; init; }

    [JsonRequired]
    public required string OperatingCountryCode { get; init; }

    [JsonRequired]
    public CountryPolicyApprovalState ApprovalState { get; init; }

    [JsonRequired]
    public DateTimeOffset EffectiveAtUtc { get; init; }

    [JsonRequired]
    public DateTimeOffset ExpiresAtUtc { get; init; }

    [JsonRequired]
    public required string[] AccommodationTypes { get; init; }

    [JsonRequired]
    public required string[] GuestCategories { get; init; }

    [JsonRequired]
    public required CountryPolicyFieldRule[] FieldRules { get; init; }

    [JsonRequired]
    public required CountryPolicyPurposeRule[] PurposeRules { get; init; }

    [JsonRequired]
    public required CountryPolicyRetentionRule[] RetentionRules { get; init; }

    [JsonRequired]
    public required CountryPolicyRightsRule RightsRule { get; init; }

    [JsonRequired]
    public required CountryPolicyRestrictionRule Restrictions { get; init; }

    [JsonRequired]
    public required string[] PermittedDataRegions { get; init; }

    [JsonRequired]
    public required string[] PermittedTransferProfiles { get; init; }

    [JsonRequired]
    public required CountryPolicyAcknowledgementRequirement[] RequiredAcknowledgements { get; init; }

    [JsonRequired]
    public required CountryPolicyApprovalMetadata Approval { get; init; }
}

public sealed record CountryPolicyFieldRule
{
    [JsonRequired]
    public required string FieldPolicyKey { get; init; }

    [JsonRequired]
    public required string GuestCategory { get; init; }

    [JsonRequired]
    public CountryPolicyFieldRequirement Requirement { get; init; }

    [JsonRequired]
    public required string[] PurposeCodes { get; init; }
}

public sealed record CountryPolicyPurposeRule
{
    [JsonRequired]
    public required string PurposeCode { get; init; }

    [JsonRequired]
    public required string[] LegalRuleReferenceKeys { get; init; }

    [JsonRequired]
    public required CountryPolicySurface[] AllowedSurfaces { get; init; }

    [JsonRequired]
    public required string[] AllowedSourceProvenance { get; init; }
}

public sealed record CountryPolicyRetentionRule
{
    [JsonRequired]
    public required string RetentionPolicyId { get; init; }

    [JsonRequired]
    public int RetentionPolicyVersion { get; init; }

    [JsonRequired]
    public required string DataClass { get; init; }

    [JsonRequired]
    public required string Trigger { get; init; }

    [JsonRequired]
    public required string Period { get; init; }
}

public sealed record CountryPolicyRightsRule
{
    [JsonRequired]
    public required string Registration { get; init; }

    [JsonRequired]
    public required string Export { get; init; }

    [JsonRequired]
    public required string Correction { get; init; }

    [JsonRequired]
    public required string Restriction { get; init; }

    [JsonRequired]
    public required string Erasure { get; init; }
}

public sealed record CountryPolicyRestrictionRule
{
    [JsonRequired]
    public required string Minors { get; init; }

    [JsonRequired]
    public required string Documents { get; init; }

    [JsonRequired]
    public required string SpecialCategoryData { get; init; }
}

public sealed record CountryPolicyAcknowledgementRequirement
{
    [JsonRequired]
    public required string AcknowledgementId { get; init; }

    [JsonRequired]
    public int AcknowledgementVersion { get; init; }
}

public sealed record CountryPolicyApprovalMetadata
{
    [JsonRequired]
    public required string OwnerReference { get; init; }

    [JsonRequired]
    public required string ReviewerReference { get; init; }

    [JsonRequired]
    public DateTimeOffset ReviewedAtUtc { get; init; }

    [JsonRequired]
    public required CountryPolicySourceReference[] Sources { get; init; }

    public string? DetachedSignatureReference { get; init; }
}

public sealed record CountryPolicySourceReference
{
    [JsonRequired]
    public required string ReferenceId { get; init; }

    [JsonRequired]
    public required string Uri { get; init; }
}

public enum CountryPolicyApprovalState
{
    Unknown = 0,
    Example = 1,
    Approved = 2
}

public enum CountryPolicyFieldRequirement
{
    Unknown = 0,
    Required = 1,
    Optional = 2,
    Prohibited = 3
}

public enum CountryPolicySurface
{
    Unknown = 0,
    PropertyActivation = 1,
    ApiWrite = 2,
    AdapterIngress = 3,
    Import = 4,
    Retention = 5,
    Export = 6,
    Correction = 7,
    Restriction = 8,
    Erasure = 9,
    Deletion = 10
}

public enum CountryPolicyRuntimeMode
{
    Engineering = 0,
    Production = 1
}

public enum CountryLaunchStatus
{
    Unknown = 0,
    Disabled = 1,
    Engineering = 2,
    Approved = 3
}
