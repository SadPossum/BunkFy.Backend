namespace BunkFy.DataGovernance;

using System.Text.Json.Serialization;

public sealed record PersonalDataCatalogDocument
{
    [JsonRequired]
    public int SchemaVersion { get; init; }

    [JsonRequired]
    public required string CatalogId { get; init; }

    [JsonRequired]
    public int CatalogVersion { get; init; }

    [JsonRequired]
    public required string Module { get; init; }

    [JsonRequired]
    public PersonalDataPolicyApprovalState ApprovalState { get; init; }

    [JsonRequired]
    public required PersonalDataAccessPolicy[] AccessPolicies { get; init; }

    [JsonRequired]
    public required PersonalDataRetentionPolicy[] RetentionPolicies { get; init; }

    [JsonRequired]
    public required PersonalDataRightsPolicy[] RightsPolicies { get; init; }

    [JsonRequired]
    public required PersonalDataFieldDefinition[] Fields { get; init; }
}

public sealed record PersonalDataAccessPolicy
{
    [JsonRequired]
    public required string Id { get; init; }

    [JsonRequired]
    public required string Scope { get; init; }

    [JsonRequired]
    public required string[] Readers { get; init; }

    [JsonRequired]
    public required string[] Writers { get; init; }
}

public sealed record PersonalDataRetentionPolicy
{
    [JsonRequired]
    public required string Id { get; init; }

    [JsonRequired]
    public PersonalDataPolicyApprovalState ApprovalState { get; init; }

    [JsonRequired]
    public required string StartsAt { get; init; }

    public string? EndsAt { get; init; }

    public string? Duration { get; init; }

    [JsonRequired]
    public required string LegalHoldBehavior { get; init; }
}

public sealed record PersonalDataRightsPolicy
{
    [JsonRequired]
    public required string Id { get; init; }

    [JsonRequired]
    public required string Export { get; init; }

    [JsonRequired]
    public required string Correction { get; init; }

    [JsonRequired]
    public required string Restriction { get; init; }

    [JsonRequired]
    public required string Erasure { get; init; }
}

public sealed record PersonalDataFieldDefinition
{
    [JsonRequired]
    public required string Id { get; init; }

    [JsonRequired]
    public PersonalDataSubjectKind DataSubject { get; init; }

    [JsonRequired]
    public PersonalDataClassification Classification { get; init; }

    [JsonRequired]
    public PersonalDataSensitivity Sensitivity { get; init; }

    [JsonRequired]
    public required string[] Purposes { get; init; }

    [JsonRequired]
    public required string[] Sources { get; init; }

    [JsonRequired]
    public required string AuthoritativeOwner { get; init; }

    [JsonRequired]
    public PersonalDataControllerContext ControllerProcessorContext { get; init; }

    [JsonRequired]
    public required string AccessPolicy { get; init; }

    [JsonRequired]
    public required string CountryPolicyKey { get; init; }

    [JsonRequired]
    public required string RetentionPolicy { get; init; }

    [JsonRequired]
    public required string RightsPolicy { get; init; }

    [JsonRequired]
    public required PersonalDataSurface[] AllowedSurfaces { get; init; }

    [JsonRequired]
    public required PersonalDataBoundary[] AllowedBoundaries { get; init; }

    [JsonRequired]
    public PersonalDataPolicyApprovalState ApprovalState { get; init; }

    [JsonRequired]
    public required PersonalDataMemberBinding[] Bindings { get; init; }
}

public sealed record PersonalDataMemberBinding
{
    [JsonRequired]
    public required string Assembly { get; init; }

    [JsonRequired]
    public required string Type { get; init; }

    [JsonRequired]
    public required string Member { get; init; }

    [JsonRequired]
    public PersonalDataSurface Surface { get; init; }

    public string? RetentionPolicy { get; init; }
}

public enum PersonalDataPolicyApprovalState
{
    Unknown = 0,
    EngineeringDefault = 1,
    Approved = 2
}

public enum PersonalDataSubjectKind
{
    Unknown = 0,
    Guest = 1,
    Staff = 2,
    AccountHolder = 3
}

public enum PersonalDataClassification
{
    Unknown = 0,
    DirectIdentifier = 1,
    Contact = 2,
    Demographic = 3,
    Preference = 4,
    FreeText = 5,
    LinkedOperational = 6,
    Lifecycle = 7,
    AuditAttribution = 8,
    SearchInput = 9
}

public enum PersonalDataSensitivity
{
    Unknown = 0,
    Standard = 1,
    Elevated = 2,
    Unstructured = 3
}

public enum PersonalDataControllerContext
{
    Unknown = 0,
    CustomerControllerBunkFyProcessor = 1,
    BunkFyController = 2,
    JointOrUndetermined = 3
}

public enum PersonalDataSurface
{
    Unknown = 0,
    ApiInput = 1,
    ApplicationCommand = 2,
    ApplicationQuery = 3,
    Persistence = 4,
    SearchIndex = 5,
    ApiResponse = 6,
    AdminInput = 7,
    AdminOutput = 8,
    ProjectionExport = 9,
    IntegrationCommand = 10,
    IntegrationEvent = 11,
    Notification = 12,
    Log = 13,
    Metric = 14,
    Trace = 15,
    Cache = 16,
    SupportBundle = 17,
    AdapterIngress = 18,
    FileIngress = 19
}

public enum PersonalDataBoundary
{
    Unknown = 0,
    IntraModule = 1,
    CustomerApi = 2,
    CrossModule = 3,
    Adapter = 4,
    Processor = 5,
    CrossRegion = 6,
    Support = 7
}

public enum PersonalDataCatalogValidationMode
{
    Engineering = 0,
    Production = 1
}
