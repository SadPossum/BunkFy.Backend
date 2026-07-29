namespace BunkFy.Modules.DataRights.Tests.Contracts;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsCorrectionContractTests
{
    private static readonly JsonSerializerOptions TransportJsonOptions =
        new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Applied_event_normalizes_fields_and_computes_deterministic_proof()
    {
        Guid eventId = Guid.NewGuid();
        Guid executionId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        Guid caseId = Guid.NewGuid();
        Guid recordId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        DataRightsCorrectionAppliedIntegrationEvent first = Create(
            eventId,
            executionId,
            propertyId,
            caseId,
            recordId,
            receiptId,
            ["guest.profile.email", "guest.profile.display-name", "guest.profile.email"]);
        DataRightsCorrectionAppliedIntegrationEvent reordered = Create(
            eventId,
            executionId,
            propertyId,
            caseId,
            recordId,
            receiptId,
            ["GUEST.PROFILE.DISPLAY-NAME", " guest.profile.email "]);

        Assert.Equal(
            ["guest.profile.display-name", "guest.profile.email"],
            first.ChangedFieldKeys);
        Assert.Equal(first.ChangedFieldsSha256, reordered.ChangedFieldsSha256);
        Assert.Equal(first.ReceiptSha256, reordered.ReceiptSha256);
        Assert.Equal(DataRightsCorrectionContract.Sha256Length, first.ReceiptSha256.Length);
        Assert.DoesNotContain(
            first.GetType().GetProperties(),
            property => property.Name is
                "DisplayName" or "LegalName" or "Email" or "Phone" or "Notes");
    }

    [Fact]
    public void Applied_event_rejects_unbounded_or_non_contract_field_keys()
    {
        Assert.Throws<ArgumentException>(() => Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ["guest.profile.email address"]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            []));
    }

    [Fact]
    public void Applied_event_round_trips_through_transport_json()
    {
        DataRightsCorrectionAppliedIntegrationEvent expected = Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ["guest.profile.display-name", "guest.profile.email"]);

        string payload = JsonSerializer.Serialize(expected, TransportJsonOptions);
        DataRightsCorrectionAppliedIntegrationEvent? actual =
            JsonSerializer.Deserialize<DataRightsCorrectionAppliedIntegrationEvent>(
                payload,
                TransportJsonOptions);

        Assert.NotNull(actual);
        Assert.Equal(expected.EventId, actual.EventId);
        Assert.Equal(expected.TenantId, actual.TenantId);
        Assert.Equal(expected.OccurredAtUtc, actual.OccurredAtUtc);
        Assert.Equal(expected.ExecutionId, actual.ExecutionId);
        Assert.Equal(expected.PropertyId, actual.PropertyId);
        Assert.Equal(expected.CaseId, actual.CaseId);
        Assert.Equal(expected.ApprovalRevision, actual.ApprovalRevision);
        Assert.Equal(expected.OwnerKey, actual.OwnerKey);
        Assert.Equal(expected.RecordType, actual.RecordType);
        Assert.Equal(expected.RecordId, actual.RecordId);
        Assert.Equal(expected.SelectedRecordVersion, actual.SelectedRecordVersion);
        Assert.Equal(expected.CurrentRecordVersion, actual.CurrentRecordVersion);
        Assert.Equal(expected.FieldPolicyKey, actual.FieldPolicyKey);
        Assert.Equal(expected.ReceiptContractVersion, actual.ReceiptContractVersion);
        Assert.Equal(expected.ReceiptId, actual.ReceiptId);
        Assert.Equal(expected.ChangedFieldKeys, actual.ChangedFieldKeys);
        Assert.Equal(expected.ChangedFieldsSha256, actual.ChangedFieldsSha256);
        Assert.Equal(expected.ReceiptSha256, actual.ReceiptSha256);
    }

    [Fact]
    public void Tenant_applied_event_is_scope_explicit_and_round_trips()
    {
        DataRightsTenantCorrectionAppliedIntegrationEvent expected = new(
            Guid.NewGuid(),
            "tenant-a",
            Now,
            Guid.NewGuid(),
            DataRightsCaseType.StaffRights,
            Guid.NewGuid(),
            approvalRevision: 4,
            "staff",
            "staff-member",
            Guid.NewGuid(),
            selectedRecordVersion: 7,
            currentRecordVersion: 8,
            "staff.staff-member.correction.v1",
            receiptContractVersion: 1,
            Guid.NewGuid(),
            ["staff.profile.work-email", "staff.profile.display-name"]);

        string payload = JsonSerializer.Serialize(expected, TransportJsonOptions);
        DataRightsTenantCorrectionAppliedIntegrationEvent? actual =
            JsonSerializer.Deserialize<DataRightsTenantCorrectionAppliedIntegrationEvent>(
                payload,
                TransportJsonOptions);

        Assert.NotNull(actual);
        Assert.Equal(DataRightsCaseType.StaffRights, actual.CaseType);
        Assert.Equal(expected.ExecutionId, actual.ExecutionId);
        Assert.Equal(expected.CaseId, actual.CaseId);
        Assert.Equal(expected.ChangedFieldKeys, actual.ChangedFieldKeys);
        Assert.Equal(expected.ChangedFieldsSha256, actual.ChangedFieldsSha256);
        Assert.Equal(expected.ReceiptSha256, actual.ReceiptSha256);
        Assert.DoesNotContain(
            actual.GetType().GetProperties(),
            property => property.Name == "PropertyId");
    }

    private static DataRightsCorrectionAppliedIntegrationEvent Create(
        Guid eventId,
        Guid executionId,
        Guid propertyId,
        Guid caseId,
        Guid recordId,
        Guid receiptId,
        IReadOnlyCollection<string> fields) => new(
        eventId,
        "tenant-a",
        Now,
        executionId,
        propertyId,
        caseId,
        approvalRevision: 6,
        "guests",
        "guest-profile",
        recordId,
        selectedRecordVersion: 3,
        currentRecordVersion: 4,
        "guests.guest-profile.correction.v1",
        receiptContractVersion: 1,
        receiptId,
        fields);
}
