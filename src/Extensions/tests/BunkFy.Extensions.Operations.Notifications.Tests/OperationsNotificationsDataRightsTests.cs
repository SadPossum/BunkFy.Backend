namespace BunkFy.Extensions.Operations.Notifications.Tests;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Gma.Modules.Notifications.Application.Ports;
using Gma.Modules.Notifications.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

[Trait("Category", "Unit")]
public sealed class OperationsNotificationsDataRightsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 20, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId =
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly string ScopeId = TenantId.ToString("D");
    private static readonly Guid PropertyId =
        Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ReservationId =
        Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public void Reservation_reference_is_deterministic_and_scope_exact()
    {
        NotificationHistoryReference expected =
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                ScopeId,
                PropertyId,
                ReservationId);

        Assert.Equal(
            expected,
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                ScopeId,
                PropertyId,
                ReservationId));
        Assert.NotEqual(
            expected,
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                Guid.NewGuid().ToString("D"),
                PropertyId,
                ReservationId));
        Assert.NotEqual(
            expected,
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                ScopeId,
                Guid.NewGuid(),
                ReservationId));
        Assert.NotEqual(
            expected,
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                ScopeId,
                PropertyId,
                Guid.NewGuid()));
        Assert.Equal(
            OperationsNotificationsDataRightsCoordinates
                .ReservationHistoryReferenceNamespace,
            expected.Namespace);
    }

    [Fact]
    public async Task Anonymisation_companion_prepares_empty_history()
    {
        var lifecycle = new TestLifecycle
        {
            EnsureOpen = (_, _, _) => Task.FromResult(
                new NotificationHistoryReferenceSnapshot(
                    NotificationHistoryReferenceStatus.Open,
                    1,
                    0,
                    0))
        };
        var contributor =
            new OperationsNotificationsReservationAnonymisationCompanionContributor(
                lifecycle,
                new TestScopeContext(),
                NullLogger<
                    OperationsNotificationsReservationAnonymisationCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                CompanionRequest(DataRightsOperation.Anonymisation),
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Completed,
            result.Status);
        DataRightsSubjectCoordinate coordinate =
            Assert.Single(result.Coordinates);
        Assert.Equal(
            OperationsNotificationsDataRightsCoordinates.Owner,
            coordinate.OwnerKey);
        Assert.Equal(
            OperationsNotificationsDataRightsCoordinates
                .ReservationHistoryRecordType,
            coordinate.RecordType);
        Assert.Equal(ReservationId, coordinate.RecordId);
        Assert.Equal(1, coordinate.RecordVersion);
        Assert.Equal(1, lifecycle.EnsureOpenCalls);
    }

    [Fact]
    public async Task Access_export_companion_freezes_empty_history()
    {
        var lifecycle = new TestLifecycle
        {
            EnsureOpen = (_, _, _) => Task.FromResult(
                new NotificationHistoryReferenceSnapshot(
                    NotificationHistoryReferenceStatus.Open,
                    1,
                    0,
                    0))
        };
        var contributor =
            new OperationsNotificationsReservationAccessExportCompanionContributor(
                lifecycle,
                new TestScopeContext(),
                NullLogger<
                    OperationsNotificationsReservationAccessExportCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                CompanionRequest(DataRightsOperation.AccessExport),
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Completed,
            result.Status);
        DataRightsSubjectCoordinate coordinate =
            Assert.Single(result.Coordinates);
        Assert.Equal(ReservationId, coordinate.RecordId);
        Assert.Equal(1, coordinate.RecordVersion);
        Assert.Equal(1, lifecycle.EnsureOpenCalls);
        Assert.Equal(0, lifecycle.SnapshotCalls);
    }

    [Fact]
    public async Task Discovery_validation_cannot_cross_property_scope()
    {
        NotificationHistoryReference expected =
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                ScopeId,
                PropertyId,
                ReservationId);
        var lifecycle = new TestLifecycle
        {
            Snapshot = (_, reference, _) => Task.FromResult(
                reference == expected
                    ? new NotificationHistoryReferenceSnapshot(
                        NotificationHistoryReferenceStatus.Open,
                        4,
                        1,
                        10)
                    : new NotificationHistoryReferenceSnapshot(
                        NotificationHistoryReferenceStatus.Missing,
                        0,
                        0,
                        0))
        };
        var contributor =
            new OperationsNotificationsDataRightsDiscoveryContributor(
                lifecycle,
                new TestScopeContext(),
                NullLogger<
                    OperationsNotificationsDataRightsDiscoveryContributor>
                    .Instance);
        var coordinate = new DataRightsSubjectCoordinate(
            OperationsNotificationsDataRightsCoordinates.Owner,
            OperationsNotificationsDataRightsCoordinates
                .ReservationHistoryRecordType,
            ReservationId,
            4);

        DataRightsSubjectSelectionValidation valid =
            await contributor.ValidateSelectionAsync(
                new DataRightsSubjectSelectionRequest(
                    ScopeId,
                    DataRightsCaseType.GuestRights,
                    PropertyId,
                    coordinate),
                CancellationToken.None);
        DataRightsSubjectSelectionValidation wrongProperty =
            await contributor.ValidateSelectionAsync(
                new DataRightsSubjectSelectionRequest(
                    ScopeId,
                    DataRightsCaseType.GuestRights,
                    Guid.NewGuid(),
                    coordinate),
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectSelectionValidationStatus.Valid,
            valid.Status);
        Assert.Equal(
            DataRightsSubjectSelectionValidationStatus.NotFound,
            wrongProperty.Status);
    }

    [Fact]
    public async Task Export_buffers_until_reference_version_is_stable()
    {
        int snapshots = 0;
        NotificationHistoryReferenceRecord record =
            CreateNotificationRecord(7);
        var lifecycle = new TestLifecycle
        {
            Snapshot = (_, _, _) =>
            {
                snapshots++;
                return Task.FromResult(
                    new NotificationHistoryReferenceSnapshot(
                        NotificationHistoryReferenceStatus.Open,
                        snapshots == 1 ? 2 : 3,
                        1,
                        7));
            },
            Page = (_, _, _, _, _) => Task.FromResult(
                new NotificationHistoryReferencePage(
                    NotificationHistoryReferenceStatus.Open,
                    2,
                    [record],
                    7,
                    false))
        };
        var sink = new CapturingSink();
        var contributor =
            new OperationsNotificationsDataRightsExportContributor(
                lifecycle,
                new TestScopeContext());

        DataRightsSubjectExportResult result =
            await contributor.ExportAsync(
                ExportRequest(version: 2),
                sink,
                CancellationToken.None);

        Assert.Equal(DataRightsSubjectExportStatus.Stale, result.Status);
        Assert.Empty(sink.Records);
    }

    [Fact]
    public async Task Export_writes_catalogued_copy_without_staff_recipient()
    {
        NotificationHistoryReferenceRecord record =
            CreateNotificationRecord(7);
        var lifecycle = new TestLifecycle
        {
            Snapshot = (_, _, _) => Task.FromResult(
                new NotificationHistoryReferenceSnapshot(
                    NotificationHistoryReferenceStatus.Open,
                    2,
                    1,
                    7)),
            Page = (_, _, _, _, _) => Task.FromResult(
                new NotificationHistoryReferencePage(
                    NotificationHistoryReferenceStatus.Open,
                    2,
                    [record],
                    7,
                    false))
        };
        var sink = new CapturingSink();
        var contributor =
            new OperationsNotificationsDataRightsExportContributor(
                lifecycle,
                new TestScopeContext());

        DataRightsSubjectExportResult result =
            await contributor.ExportAsync(
                ExportRequest(version: 2),
                sink,
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectExportStatus.Succeeded,
            result.Status);
        Assert.Equal(1, result.RecordCount);
        DataRightsExportRecord exported = Assert.Single(sink.Records);
        Assert.Equal(
            OperationsNotificationsDataRightsCoordinates
                .NotificationCopyRecordType,
            exported.RecordType);
        Assert.Equal(record.NotificationId, exported.RecordId);
        Assert.Equal(record.StreamSequence, exported.RecordVersion);
        Assert.Equal(13, exported.Fields.Count);
        Assert.DoesNotContain(
            exported.Fields,
            field => field.FieldId.Contains(
                "recipient",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Export_rejects_an_oversized_field_before_writing()
    {
        NotificationHistoryReferenceRecord record =
            CreateNotificationRecord(7) with
            {
                Payload = JsonSerializer.SerializeToElement(
                    new
                    {
                        PropertyId,
                        ReservationId,
                        Value = new string(
                            'x',
                            DataRightsExportLimits.MaxFieldValueBytes)
                    })
            };
        var lifecycle = new TestLifecycle
        {
            Snapshot = (_, _, _) => Task.FromResult(
                new NotificationHistoryReferenceSnapshot(
                    NotificationHistoryReferenceStatus.Open,
                    2,
                    1,
                    7)),
            Page = (_, _, _, _, _) => Task.FromResult(
                new NotificationHistoryReferencePage(
                    NotificationHistoryReferenceStatus.Open,
                    2,
                    [record],
                    7,
                    false))
        };
        var sink = new CapturingSink();
        var contributor =
            new OperationsNotificationsDataRightsExportContributor(
                lifecycle,
                new TestScopeContext());

        DataRightsSubjectExportResult result =
            await contributor.ExportAsync(
                ExportRequest(version: 2),
                sink,
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectExportStatus.ScopeUnavailable,
            result.Status);
        Assert.Empty(sink.Records);
    }

    [Fact]
    public async Task Anonymisation_maps_close_receipt_to_stable_owner_proof()
    {
        Guid workItemId = Guid.NewGuid();
        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                ScopeId,
                PropertyId,
                ReservationId);
        NotificationHistoryReferenceCloseReceipt receipt =
            CreateCloseReceipt(workItemId, reference, 3);
        var lifecycle = new TestLifecycle
        {
            Close = (request, _) =>
            {
                Assert.Equal(
                    OperationsNotificationsDataRightsReceipt.MaximumRecords,
                    request.MaximumRecords);
                return Task.FromResult(
                    new NotificationHistoryReferenceCloseResult(
                        NotificationHistoryReferenceCloseStatus.Completed,
                        receipt));
            }
        };
        var contributor =
            new OperationsNotificationsDataRightsAnonymisationContributor(
                lifecycle,
                new TestScopeContext(),
                new FixedClock());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                AnonymisationRequest(workItemId, version: 2),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Completed,
            result.Status);
        DataRightsAnonymisationOwnerProof proof =
            Assert.IsType<DataRightsAnonymisationOwnerProof>(
                result.OwnerProof);
        Assert.Equal(receipt.OperationId, proof.ReceiptId);
        Assert.Equal(receipt.ResultingVersion, proof.ResultingRecordVersion);
        Assert.Equal(
            OperationsNotificationsDataRightsReceipt.ComputeSha256(receipt),
            proof.ReceiptSha256);
    }

    [Fact]
    public async Task Restore_accepts_exact_replayed_owner_receipt()
    {
        Guid operationId = Guid.NewGuid();
        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates.ForReservation(
                ScopeId,
                PropertyId,
                ReservationId);
        NotificationHistoryReferenceCloseReceipt receipt =
            CreateCloseReceipt(operationId, reference, 3);
        string ownerReceiptSha =
            OperationsNotificationsDataRightsReceipt.ComputeSha256(receipt);
        var lifecycle = new TestLifecycle
        {
            Close = (request, _) =>
            {
                Assert.Equal(
                    OperationsNotificationsDataRightsReceipt.MaximumRecords,
                    request.MaximumRecords);
                return Task.FromResult(
                    new NotificationHistoryReferenceCloseResult(
                        NotificationHistoryReferenceCloseStatus.Replayed,
                        receipt));
            }
        };
        var contributor =
            new OperationsNotificationsDataRightsAnonymisationRestoreContributor(
                lifecycle,
                new TestScopeContext(),
                new FixedClock());

        DataRightsAnonymisationRestoreResult result =
            await contributor.RestoreAsync(
                new DataRightsAnonymisationRestoreRequest(
                    DataRightsAnonymisationRestoreContract.CurrentVersion,
                    ScopeId,
                    Guid.NewGuid(),
                    1,
                    new string('a', 64),
                    PropertyId,
                    OperationsNotificationsDataRightsCoordinates.Owner,
                    OperationsNotificationsDataRightsCoordinates
                        .ReservationHistoryRecordType,
                    ReservationId,
                    OperationsNotificationsDataRightsReceipt.ContractVersion,
                    operationId,
                    ownerReceiptSha,
                    3,
                    Now),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationRestoreStatus.Completed,
            result.Status);
        Assert.Equal(ownerReceiptSha, result.Proof!.OwnerReceiptSha256);
        Assert.Equal(3, result.Proof.ResultingRecordVersion);
        Assert.Equal(3, result.Proof.TombstoneRevision);
    }

    [Fact]
    public void Dependency_injection_registers_each_data_rights_capability_once()
    {
        ServiceCollection services = [];

        services.AddBunkFyOperationsNotifications();

        Assert.Equal(
            2,
            services.Count(descriptor =>
                descriptor.ServiceType ==
                typeof(IDataRightsRequiredCompanionContributor)));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType ==
            typeof(IDataRightsSubjectDiscoveryContributor));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType ==
            typeof(IDataRightsSubjectExportContributor));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType ==
            typeof(IDataRightsAnonymisationContributor));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType ==
            typeof(IDataRightsAnonymisationRestoreContributor));
    }

    private static DataRightsRequiredCompanionRequest CompanionRequest(
        DataRightsOperation operation) =>
        new(
            DataRightsRequiredCompanionContract.CurrentVersion,
            ScopeId,
            DataRightsCaseType.GuestRights,
            operation,
            PropertyId,
            Guid.NewGuid(),
            new DataRightsSubjectCoordinate(
                ReservationsDataRightsCoordinates.Owner,
                ReservationsDataRightsCoordinates.ReservationRecordType,
                ReservationId,
                5),
            4);

    private static DataRightsSubjectExportRequest ExportRequest(
        long version) =>
        new(
            ScopeId,
            DataRightsCaseType.GuestRights,
            PropertyId,
            new DataRightsSubjectCoordinate(
                OperationsNotificationsDataRightsCoordinates.Owner,
                OperationsNotificationsDataRightsCoordinates
                    .ReservationHistoryRecordType,
                ReservationId,
                version));

    private static DataRightsAnonymisationContributionRequest
        AnonymisationRequest(Guid workItemId, long version) =>
        new(
            DataRightsAnonymisationContract.CurrentVersion,
            ScopeId,
            workItemId,
            Guid.NewGuid(),
            PropertyId,
            Guid.NewGuid(),
            1,
            1,
            new DataRightsSubjectCoordinate(
                OperationsNotificationsDataRightsCoordinates.Owner,
                OperationsNotificationsDataRightsCoordinates
                    .ReservationHistoryRecordType,
                ReservationId,
                version),
            new DataRightsApprovalEvidence(
                1,
                PropertyId,
                1,
                "US",
                "guest-rights-policy",
                1,
                "guest-retention-policy",
                1,
                new string('b', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                Now.AddMinutes(-1),
                true),
            "user:executor",
            Now.AddMinutes(5));

    private static NotificationHistoryReferenceRecord
        CreateNotificationRecord(long sequence)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(
            new ReservationNotificationPayload(
                PropertyId,
                ReservationId));
        return new NotificationHistoryReferenceRecord(
            Guid.NewGuid(),
            "staff-recipient",
            ReservationsModuleMetadata.Name,
            "reservation-cancelled",
            1,
            "Reservation cancelled",
            "A reservation was cancelled.",
            NotificationSeverity.Warning,
            sequence,
            Now.AddMinutes(-2),
            Now.AddMinutes(-1),
            payload,
            ["domain:reservations", "web"],
            NotificationDeliveryPolicy.RespectPreferences);
    }

    private static NotificationHistoryReferenceCloseReceipt
        CreateCloseReceipt(
            Guid operationId,
            NotificationHistoryReference reference,
            long resultingVersion) =>
        new(
            operationId,
            reference,
            resultingVersion,
            1,
            new string('d', 64),
            Now);

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => OperationsNotificationsDataRightsTests.ScopeId;
    }

    private sealed class FixedClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class CapturingSink : IDataRightsExportSink
    {
        public List<DataRightsExportRecord> Records { get; } = [];

        public ValueTask WriteAsync(
            DataRightsExportRecord record,
            CancellationToken cancellationToken)
        {
            this.Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestLifecycle : INotificationHistoryLifecycle
    {
        public Func<
            string,
            NotificationHistoryReference,
            CancellationToken,
            Task<NotificationHistoryReferenceSnapshot>> EnsureOpen
        { get; init; } =
            (_, _, _) => Task.FromResult(
                new NotificationHistoryReferenceSnapshot(
                    NotificationHistoryReferenceStatus.Missing,
                    0,
                    0,
                    0));

        public Func<
            string,
            NotificationHistoryReference,
            CancellationToken,
            Task<NotificationHistoryReferenceSnapshot>> Snapshot
        { get; init; } =
            (_, _, _) => Task.FromResult(
                new NotificationHistoryReferenceSnapshot(
                    NotificationHistoryReferenceStatus.Missing,
                    0,
                    0,
                    0));

        public Func<
            string,
            NotificationHistoryReference,
            long,
            int,
            CancellationToken,
            Task<NotificationHistoryReferencePage>> Page
        { get; init; } =
            (_, _, cursor, _, _) => Task.FromResult(
                new NotificationHistoryReferencePage(
                    NotificationHistoryReferenceStatus.Missing,
                    0,
                    [],
                    cursor,
                    false));

        public Func<
            NotificationHistoryReferenceCloseRequest,
            CancellationToken,
            Task<NotificationHistoryReferenceCloseResult>> Close
        { get; init; } =
            (_, _) => Task.FromResult(
                new NotificationHistoryReferenceCloseResult(
                    NotificationHistoryReferenceCloseStatus.Invalid,
                    null));

        public int EnsureOpenCalls { get; private set; }
        public int SnapshotCalls { get; private set; }

        public Task<NotificationHistoryReferenceSnapshot> EnsureOpenAsync(
            string scopeId,
            NotificationHistoryReference reference,
            CancellationToken cancellationToken)
        {
            this.EnsureOpenCalls++;
            return this.EnsureOpen(scopeId, reference, cancellationToken);
        }

        public Task<NotificationHistoryReferenceSnapshot> GetSnapshotAsync(
            string scopeId,
            NotificationHistoryReference reference,
            CancellationToken cancellationToken)
        {
            this.SnapshotCalls++;
            return this.Snapshot(scopeId, reference, cancellationToken);
        }

        public Task<NotificationHistoryReferencePage> ListAsync(
            string scopeId,
            NotificationHistoryReference reference,
            long afterStreamSequence,
            int pageSize,
            CancellationToken cancellationToken) =>
            this.Page(
                scopeId,
                reference,
                afterStreamSequence,
                pageSize,
                cancellationToken);

        public Task<NotificationHistoryReferenceCloseResult> CloseAsync(
            NotificationHistoryReferenceCloseRequest request,
            CancellationToken cancellationToken) =>
            this.Close(request, cancellationToken);
    }
}
