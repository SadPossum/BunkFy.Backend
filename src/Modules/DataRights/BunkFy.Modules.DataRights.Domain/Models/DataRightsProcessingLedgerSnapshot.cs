namespace BunkFy.Modules.DataRights.Domain.Models;

using System.Text.Json.Serialization;

public sealed record DataRightsProcessingLedgerSnapshot(
    int ContractVersion,
    Guid EntryId,
    string ScopeId,
    long TenantSequence,
    Guid WorkItemId,
    Guid CaseId,
    long ApprovalRevision,
    long OperationRevision,
    DataRightsCaseOperation Operation,
    Guid? RoutingPropertyId,
    string OwnerKey,
    string RecordType,
    int RecordPseudonymKeyVersion,
    string RecordPseudonymSha256,
    string DispositionCode,
    string ReasonCode,
    DateTimeOffset CompletedAtUtc,
    int PolicyEvidenceSchemaVersion,
    string PolicyId,
    int PolicyVersion,
    string PolicyContentSha256,
    string RetentionPolicyId,
    int RetentionPolicyVersion,
    int OwnerReceiptContractVersion,
    Guid OwnerReceiptId,
    string OwnerReceiptSha256,
    string PreviousEntrySha256,
    string EntrySha256,
    Guid? ReplayOfLedgerEntryId,
    Guid? SupersedesLedgerEntryId,
    long? ResultingRecordVersion = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    DataRightsCaseKind CaseKind = DataRightsCaseKind.Unknown,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    DataRightsCaseScopeKind ScopeKind = DataRightsCaseScopeKind.Unknown,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    long? PolicyPropertyVersion = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    string? PolicyOperatingCountryCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    string? PolicyPurposeCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    string? PolicySurface = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    string? PolicySourceProvenance = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    string? PolicyRetentionDataClass = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    string? PolicyRetentionTrigger = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    DateTimeOffset? PolicyRetentionTriggeredAtUtc = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    DateTimeOffset? PolicyRetentionDeadlineUtc = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    DateTimeOffset? PolicyEvaluatedAtUtc = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    string? PolicyStateBindingsJson = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    string? PolicyStateBindingsSha256 = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool? PolicyRequiresDistinctExecutor = null);
