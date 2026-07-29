namespace BunkFy.Modules.DataRights.Contracts;

using System.Buffers;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using Gma.Framework.Messaging;
using Gma.Framework.Tenancy;
using Gma.Framework.Tenancy.Messaging;

[IntegrationEventName(EventType)]
[IntegrationEventVersion(EventVersion)]
[TenantScoped]
public sealed record DataRightsTenantCorrectionAppliedIntegrationEvent
    : TenantIntegrationEvent
{
    public const string EventType = "data-rights-tenant-correction-applied";
    public const int EventVersion = 1;

    public DataRightsTenantCorrectionAppliedIntegrationEvent(
        Guid eventId,
        string tenantId,
        DateTimeOffset occurredAtUtc,
        Guid executionId,
        DataRightsCaseType caseType,
        Guid caseId,
        long approvalRevision,
        string ownerKey,
        string recordType,
        Guid recordId,
        long selectedRecordVersion,
        long currentRecordVersion,
        string fieldPolicyKey,
        int receiptContractVersion,
        Guid receiptId,
        IReadOnlyCollection<string> changedFieldKeys)
        : base(eventId, tenantId, occurredAtUtc, EventType, EventVersion)
    {
        this.ExecutionId = IntegrationEventContractGuards.RequireId(
            executionId,
            nameof(executionId));
        this.CaseId = IntegrationEventContractGuards.RequireId(caseId, nameof(caseId));
        this.RecordId = IntegrationEventContractGuards.RequireId(recordId, nameof(recordId));
        this.ReceiptId = IntegrationEventContractGuards.RequireId(receiptId, nameof(receiptId));
        if (caseType is not DataRightsCaseType.StaffRights
            and not DataRightsCaseType.TenantTermination)
        {
            throw new ArgumentOutOfRangeException(nameof(caseType));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(approvalRevision, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(selectedRecordVersion, 1);
        ArgumentOutOfRangeException.ThrowIfNotEqual(
            currentRecordVersion,
            selectedRecordVersion + 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(receiptContractVersion, 1);

        this.CaseType = caseType;
        this.OwnerKey = NormalizeKey(
            ownerKey,
            DataRightsCorrectionContract.OwnerKeyMaxLength,
            nameof(ownerKey));
        this.RecordType = NormalizeKey(
            recordType,
            DataRightsCorrectionContract.RecordTypeMaxLength,
            nameof(recordType));
        this.FieldPolicyKey = NormalizeKey(
            fieldPolicyKey,
            DataRightsCorrectionContract.FieldPolicyKeyMaxLength,
            nameof(fieldPolicyKey));
        this.ChangedFieldKeys = NormalizeChangedFields(changedFieldKeys);
        this.ApprovalRevision = approvalRevision;
        this.SelectedRecordVersion = selectedRecordVersion;
        this.CurrentRecordVersion = currentRecordVersion;
        this.ReceiptContractVersion = receiptContractVersion;
        this.ChangedFieldsSha256 = ComputeChangedFieldsSha256(this.ChangedFieldKeys);
        this.ReceiptSha256 = ComputeReceiptSha256(this);
    }

    public Guid ExecutionId { get; }
    public DataRightsCaseType CaseType { get; }
    public Guid CaseId { get; }
    public long ApprovalRevision { get; }
    public string OwnerKey { get; }
    public string RecordType { get; }
    public Guid RecordId { get; }
    public long SelectedRecordVersion { get; }
    public long CurrentRecordVersion { get; }
    public string FieldPolicyKey { get; }
    public int ReceiptContractVersion { get; }
    public Guid ReceiptId { get; }
    public IReadOnlyCollection<string> ChangedFieldKeys { get; }
    public string ChangedFieldsSha256 { get; }
    public string ReceiptSha256 { get; }

    private static string NormalizeKey(string value, int maxLength, string parameterName)
    {
        string normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maxLength ||
            normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '.' and not '-'))
        {
            throw new ArgumentException(
                "A bounded lowercase contract key is required.",
                parameterName);
        }

        return normalized;
    }

    private static ReadOnlyCollection<string> NormalizeChangedFields(
        IReadOnlyCollection<string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        string[] normalized = fields
            .Select(field => NormalizeKey(
                field,
                DataRightsCorrectionContract.FieldKeyMaxLength,
                nameof(fields)))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (normalized.Length is 0 or > DataRightsCorrectionContract.MaxChangedFieldCount)
        {
            throw new ArgumentOutOfRangeException(nameof(fields));
        }

        return Array.AsReadOnly(normalized);
    }

    private static string ComputeChangedFieldsSha256(
        IReadOnlyCollection<string> fields)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartArray();
            foreach (string field in fields)
            {
                writer.WriteStringValue(field);
            }

            writer.WriteEndArray();
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan))
            .ToLowerInvariant();
    }

    private static string ComputeReceiptSha256(
        DataRightsTenantCorrectionAppliedIntegrationEvent integrationEvent)
    {
        ArrayBufferWriter<byte> buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber(
                "contractVersion",
                DataRightsCorrectionContract.CurrentVersion);
            writer.WriteString("tenantId", integrationEvent.TenantId);
            writer.WriteString("executionId", integrationEvent.ExecutionId);
            writer.WriteNumber("caseType", (int)integrationEvent.CaseType);
            writer.WriteString("caseId", integrationEvent.CaseId);
            writer.WriteNumber("approvalRevision", integrationEvent.ApprovalRevision);
            writer.WriteString("ownerKey", integrationEvent.OwnerKey);
            writer.WriteString("recordType", integrationEvent.RecordType);
            writer.WriteString("recordId", integrationEvent.RecordId);
            writer.WriteNumber(
                "selectedRecordVersion",
                integrationEvent.SelectedRecordVersion);
            writer.WriteNumber(
                "currentRecordVersion",
                integrationEvent.CurrentRecordVersion);
            writer.WriteString("fieldPolicyKey", integrationEvent.FieldPolicyKey);
            writer.WriteNumber(
                "receiptContractVersion",
                integrationEvent.ReceiptContractVersion);
            writer.WriteString("receiptId", integrationEvent.ReceiptId);
            writer.WriteString(
                "changedFieldsSha256",
                integrationEvent.ChangedFieldsSha256);
            writer.WriteEndObject();
        }

        return Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan))
            .ToLowerInvariant();
    }
}
