namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsApprovalEvidence(
    int SchemaVersion,
    Guid? PropertyId,
    long PropertyVersion,
    string OperatingCountryCode,
    string PolicyId,
    int PolicyVersion,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    string ContentSha256,
    string PurposeCode,
    string Surface,
    string SourceProvenance,
    DateTimeOffset EvaluatedAtUtc,
    bool RequiresDistinctExecutor,
    DataRightsCaseType CaseType = DataRightsCaseType.GuestRights,
    DataRightsExecutionScopeKind ScopeKind =
        DataRightsExecutionScopeKind.Property,
    string? RetentionDataClass = null,
    string? RetentionTrigger = null,
    DateTimeOffset? RetentionTriggeredAtUtc = null,
    DateTimeOffset? RetentionDeadlineUtc = null,
    IReadOnlyCollection<DataRightsApprovalEvidenceBinding>? StateBindings =
        null,
    string? StateBindingsSha256 = null)
{
    public const string EmptyStateBindingsSha256 =
        "4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945";
}
