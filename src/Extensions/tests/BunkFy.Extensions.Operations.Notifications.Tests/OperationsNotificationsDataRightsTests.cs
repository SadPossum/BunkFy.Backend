namespace BunkFy.Extensions.Operations.Notifications.Tests;

using System.Text.Json;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Staff.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
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
    private static readonly Guid StaffMemberId =
        Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid SourceLinkId =
        Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");

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
    public void Staff_reference_is_deterministic_and_survives_account_identity_changes()
    {
        NotificationHistoryReference expected =
            OperationsNotificationsDataRightsCoordinates.ForStaff(
                ScopeId,
                StaffMemberId);

        Assert.Equal(
            expected,
            OperationsNotificationsDataRightsCoordinates.ForStaff(
                ScopeId,
                StaffMemberId));
        Assert.NotEqual(
            expected,
            OperationsNotificationsDataRightsCoordinates.ForStaff(
                Guid.NewGuid().ToString("D"),
                StaffMemberId));
        Assert.NotEqual(
            expected,
            OperationsNotificationsDataRightsCoordinates.ForStaff(
                ScopeId,
                Guid.NewGuid()));
        Assert.Equal(
            OperationsNotificationsDataRightsCoordinates
                .StaffInboxHistoryReferenceNamespace,
            expected.Namespace);
    }

    [Fact]
    public void Tenant_history_reference_is_deterministic_and_scope_exact()
    {
        NotificationHistoryReference expected =
            OperationsNotificationsDataRightsCoordinates.ForTenant(ScopeId);

        Assert.Equal(
            expected,
            OperationsNotificationsDataRightsCoordinates.ForTenant(ScopeId));
        Assert.NotEqual(
            expected,
            OperationsNotificationsDataRightsCoordinates.ForTenant(
                Guid.NewGuid().ToString("D")));
        Assert.Equal(
            OperationsNotificationsDataRightsCoordinates
                .TenantInboxHistoryReferenceNamespace,
            expected.Namespace);
    }

    [Fact]
    public void Ingestion_source_link_reference_is_deterministic_and_scope_exact()
    {
        NotificationHistoryReference expected =
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    ScopeId,
                    PropertyId,
                    SourceLinkId);

        Assert.Equal(
            expected,
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    ScopeId,
                    PropertyId,
                    SourceLinkId));
        Assert.NotEqual(
            expected,
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    Guid.NewGuid().ToString("D"),
                    PropertyId,
                    SourceLinkId));
        Assert.NotEqual(
            expected,
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    ScopeId,
                    Guid.NewGuid(),
                    SourceLinkId));
        Assert.NotEqual(
            expected,
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    ScopeId,
                    PropertyId,
                    Guid.NewGuid()));
        Assert.Equal(
            OperationsNotificationsDataRightsCoordinates
                .IngestionSourceLinkHistoryReferenceNamespace,
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
    public async Task Ingestion_anonymisation_companion_freezes_exact_source_link_history()
    {
        NotificationHistoryReference expected =
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    ScopeId,
                    PropertyId,
                    SourceLinkId);
        var lifecycle = new TestLifecycle
        {
            EnsureOpen = (scopeId, reference, _) =>
            {
                Assert.Equal(ScopeId, scopeId);
                Assert.Equal(expected, reference);
                return Task.FromResult(
                    new NotificationHistoryReferenceSnapshot(
                        NotificationHistoryReferenceStatus.Open,
                        6,
                        2,
                        11));
            }
        };
        var contributor =
            new OperationsNotificationsIngestionAnonymisationCompanionContributor(
                lifecycle,
                new TestScopeContext(),
                NullLogger<
                    OperationsNotificationsIngestionAnonymisationCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                IngestionCompanionRequest(
                    DataRightsOperation.Anonymisation),
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
                .IngestionSourceLinkHistoryRecordType,
            coordinate.RecordType);
        Assert.Equal(SourceLinkId, coordinate.RecordId);
        Assert.Equal(6, coordinate.RecordVersion);
        Assert.Equal(1, lifecycle.EnsureOpenCalls);
    }

    [Fact]
    public async Task Ingestion_access_export_companion_rejects_wrong_source_coordinate()
    {
        var lifecycle = new TestLifecycle();
        var contributor =
            new OperationsNotificationsIngestionAccessExportCompanionContributor(
                lifecycle,
                new TestScopeContext(),
                NullLogger<
                    OperationsNotificationsIngestionAccessExportCompanionContributor>
                    .Instance);
        DataRightsRequiredCompanionRequest request =
            IngestionCompanionRequest(DataRightsOperation.AccessExport) with
            {
                SourceCoordinate = new DataRightsSubjectCoordinate(
                    ReservationsDataRightsCoordinates.Owner,
                    ReservationsDataRightsCoordinates.ReservationRecordType,
                    SourceLinkId,
                    5)
            };

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Blocked,
            result.Status);
        Assert.Equal(0, lifecycle.EnsureOpenCalls);
    }

    [Fact]
    public async Task Ingestion_access_export_companion_respects_case_capacity()
    {
        var lifecycle = new TestLifecycle
        {
            EnsureOpen = (_, _, _) => Task.FromResult(
                new NotificationHistoryReferenceSnapshot(
                    NotificationHistoryReferenceStatus.Open,
                    2,
                    1,
                    1))
        };
        var contributor =
            new OperationsNotificationsIngestionAccessExportCompanionContributor(
                lifecycle,
                new TestScopeContext(),
                NullLogger<
                    OperationsNotificationsIngestionAccessExportCompanionContributor>
                    .Instance);
        DataRightsRequiredCompanionRequest request =
            IngestionCompanionRequest(DataRightsOperation.AccessExport) with
            {
                RemainingSubjectCapacity = 0
            };

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Blocked,
            result.Status);
        Assert.Equal(1, lifecycle.EnsureOpenCalls);
    }

    [Fact]
    public async Task Ingestion_companion_retries_when_history_lifecycle_is_unavailable()
    {
        var lifecycle = new TestLifecycle
        {
            EnsureOpen = (_, _, _) =>
                throw new InvalidOperationException("unavailable")
        };
        var contributor =
            new OperationsNotificationsIngestionAccessExportCompanionContributor(
                lifecycle,
                new TestScopeContext(),
                NullLogger<
                    OperationsNotificationsIngestionAccessExportCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                IngestionCompanionRequest(
                    DataRightsOperation.AccessExport),
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.RetryRequired,
            result.Status);
        Assert.Equal(1, lifecycle.EnsureOpenCalls);
    }

    [Fact]
    public async Task Staff_anonymisation_companion_freezes_exact_empty_inbox_history()
    {
        NotificationHistoryReference expected =
            OperationsNotificationsDataRightsCoordinates.ForStaff(
                ScopeId,
                StaffMemberId);
        var lifecycle = new TestLifecycle
        {
            EnsureOpen = (scopeId, reference, _) =>
            {
                Assert.Equal(ScopeId, scopeId);
                Assert.Equal(expected, reference);
                return Task.FromResult(
                    new NotificationHistoryReferenceSnapshot(
                        NotificationHistoryReferenceStatus.Open,
                        3,
                        0,
                        0));
            }
        };
        var contributor =
            new OperationsNotificationsStaffAnonymisationCompanionContributor(
                lifecycle,
                new TestScopeContext(),
                NullLogger<
                    OperationsNotificationsStaffAnonymisationCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                StaffCompanionRequest(DataRightsOperation.Anonymisation),
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
                .StaffInboxHistoryRecordType,
            coordinate.RecordType);
        Assert.Equal(StaffMemberId, coordinate.RecordId);
        Assert.Equal(3, coordinate.RecordVersion);
    }

    [Fact]
    public async Task Staff_access_export_companion_respects_case_capacity()
    {
        var lifecycle = new TestLifecycle();
        var contributor =
            new OperationsNotificationsStaffAccessExportCompanionContributor(
                lifecycle,
                new TestScopeContext(),
                NullLogger<
                    OperationsNotificationsStaffAccessExportCompanionContributor>
                    .Instance);
        DataRightsRequiredCompanionRequest request =
            StaffCompanionRequest(DataRightsOperation.AccessExport) with
            {
                RemainingSubjectCapacity = 0
            };

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Blocked,
            result.Status);
        Assert.Equal(0, lifecycle.EnsureOpenCalls);
    }

    [Fact]
    public async Task Staff_companion_cannot_prepare_history_outside_the_active_scope()
    {
        var lifecycle = new TestLifecycle();
        var contributor =
            new OperationsNotificationsStaffAccessExportCompanionContributor(
                lifecycle,
                new TestScopeContext(Guid.NewGuid().ToString("D")),
                NullLogger<
                    OperationsNotificationsStaffAccessExportCompanionContributor>
                    .Instance);

        DataRightsRequiredCompanionResult result =
            await contributor.ExpandAsync(
                StaffCompanionRequest(DataRightsOperation.AccessExport),
                CancellationToken.None);

        Assert.Equal(
            DataRightsRequiredCompanionStatus.Blocked,
            result.Status);
        Assert.Equal(0, lifecycle.EnsureOpenCalls);
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
    public async Task Discovery_validates_exact_ingestion_source_link_history()
    {
        NotificationHistoryReference expected =
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    ScopeId,
                    PropertyId,
                    SourceLinkId);
        var lifecycle = new TestLifecycle
        {
            Snapshot = (scopeId, reference, _) =>
            {
                Assert.Equal(ScopeId, scopeId);
                Assert.Equal(expected, reference);
                return Task.FromResult(
                    new NotificationHistoryReferenceSnapshot(
                        NotificationHistoryReferenceStatus.Open,
                        4,
                        1,
                        10));
            }
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
                .IngestionSourceLinkHistoryRecordType,
            SourceLinkId,
            4);

        DataRightsSubjectSelectionValidation result =
            await contributor.ValidateSelectionAsync(
                new DataRightsSubjectSelectionRequest(
                    ScopeId,
                    DataRightsCaseType.GuestRights,
                    PropertyId,
                    coordinate),
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectSelectionValidationStatus.Valid,
            result.Status);
        Assert.Equal(coordinate, result.Coordinate);
        Assert.Equal(1, lifecycle.SnapshotCalls);
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
    public async Task Ingestion_export_writes_only_known_provider_attention_copy()
    {
        NotificationHistoryReferenceRecord record =
            CreateProviderAttentionRecord(9);
        NotificationHistoryReference expected =
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    ScopeId,
                    PropertyId,
                    SourceLinkId);
        var lifecycle = new TestLifecycle
        {
            Snapshot = (_, reference, _) =>
            {
                Assert.Equal(expected, reference);
                return Task.FromResult(
                    new NotificationHistoryReferenceSnapshot(
                        NotificationHistoryReferenceStatus.Open,
                        2,
                        1,
                        9));
            },
            Page = (_, reference, _, _, _) =>
            {
                Assert.Equal(expected, reference);
                return Task.FromResult(
                    new NotificationHistoryReferencePage(
                        NotificationHistoryReferenceStatus.Open,
                        2,
                        [record],
                        9,
                        false));
            }
        };
        var sink = new CapturingSink();
        var contributor =
            new OperationsNotificationsDataRightsExportContributor(
                lifecycle,
                new TestScopeContext());

        DataRightsSubjectExportResult result =
            await contributor.ExportAsync(
                IngestionExportRequest(version: 2),
                sink,
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectExportStatus.Succeeded,
            result.Status);
        Assert.Equal(1, result.RecordCount);
        DataRightsExportRecord exported = Assert.Single(sink.Records);
        Assert.Equal(record.NotificationId, exported.RecordId);
        Assert.Equal(13, exported.Fields.Count);
    }

    [Fact]
    public async Task Ingestion_export_rejects_malformed_history_without_partial_output()
    {
        NotificationHistoryReferenceRecord valid =
            CreateProviderAttentionRecord(8);
        NotificationHistoryReferenceRecord malformed =
            CreateProviderAttentionRecord(9) with
            {
                Payload = JsonSerializer.SerializeToElement(
                    new
                    {
                        PropertyId,
                        ReceiptId = Guid.NewGuid(),
                        ConnectionId = Guid.NewGuid(),
                        ReservationId = (Guid?)null,
                        Unexpected = "not-catalogued"
                    })
            };
        var lifecycle = new TestLifecycle
        {
            Snapshot = (_, _, _) => Task.FromResult(
                new NotificationHistoryReferenceSnapshot(
                    NotificationHistoryReferenceStatus.Open,
                    2,
                    2,
                    9)),
            Page = (_, _, _, _, _) => Task.FromResult(
                new NotificationHistoryReferencePage(
                    NotificationHistoryReferenceStatus.Open,
                    2,
                    [valid, malformed],
                    9,
                    false))
        };
        var sink = new CapturingSink();
        var contributor =
            new OperationsNotificationsDataRightsExportContributor(
                lifecycle,
                new TestScopeContext());

        DataRightsSubjectExportResult result =
            await contributor.ExportAsync(
                IngestionExportRequest(version: 2),
                sink,
                CancellationToken.None);

        Assert.Equal(
            DataRightsSubjectExportStatus.ScopeUnavailable,
            result.Status);
        Assert.Empty(sink.Records);
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
    public async Task Ingestion_anonymisation_uses_source_link_history_reason()
    {
        Guid workItemId = Guid.NewGuid();
        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    ScopeId,
                    PropertyId,
                    SourceLinkId);
        NotificationHistoryReferenceCloseReceipt receipt =
            CreateCloseReceipt(workItemId, reference, 3);
        var lifecycle = new TestLifecycle
        {
            Close = (request, _) =>
            {
                Assert.Equal(reference, request.Reference);
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
                IngestionAnonymisationRequest(workItemId, version: 2),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Completed,
            result.Status);
        Assert.Equal(
            OperationsNotificationsDataRightsReceipt.IngestionReasonCode,
            result.OwnerProof!.ReasonCode);
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
    public async Task Ingestion_restore_accepts_exact_replayed_owner_receipt()
    {
        Guid operationId = Guid.NewGuid();
        NotificationHistoryReference reference =
            OperationsNotificationsDataRightsCoordinates
                .ForIngestionSourceLink(
                    ScopeId,
                    PropertyId,
                    SourceLinkId);
        NotificationHistoryReferenceCloseReceipt receipt =
            CreateCloseReceipt(operationId, reference, 3);
        string ownerReceiptSha =
            OperationsNotificationsDataRightsReceipt.ComputeSha256(receipt);
        var lifecycle = new TestLifecycle
        {
            Close = (request, _) =>
            {
                Assert.Equal(reference, request.Reference);
                return Task.FromResult(
                    new NotificationHistoryReferenceCloseResult(
                        NotificationHistoryReferenceCloseStatus.Replayed,
                        receipt));
            }
        };
        var contributor =
            new OperationsNotificationsIngestionDataRightsAnonymisationRestoreContributor(
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
                        .IngestionSourceLinkHistoryRecordType,
                    SourceLinkId,
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
    }

    [Fact]
    public void Dependency_injection_registers_each_data_rights_capability_once()
    {
        ServiceCollection services = [];

        services.AddBunkFyOperationsNotifications();

        Assert.Equal(
            6,
            services.Count(descriptor =>
                descriptor.ServiceType ==
                typeof(IDataRightsRequiredCompanionContributor)));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType ==
            typeof(IDataRightsSubjectDiscoveryContributor));
        Assert.Equal(
            2,
            services.Count(descriptor =>
                descriptor.ServiceType ==
                typeof(IDataRightsSubjectExportContributor)));
        Assert.Single(services, descriptor =>
            descriptor.ServiceType ==
            typeof(IDataRightsAnonymisationContributor));
        Assert.Equal(
            2,
            services.Count(descriptor =>
                descriptor.ServiceType ==
                typeof(IDataRightsAnonymisationRestoreContributor)));
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

    private static DataRightsSubjectExportRequest IngestionExportRequest(
        long version) =>
        new(
            ScopeId,
            DataRightsCaseType.GuestRights,
            PropertyId,
            new DataRightsSubjectCoordinate(
                OperationsNotificationsDataRightsCoordinates.Owner,
                OperationsNotificationsDataRightsCoordinates
                    .IngestionSourceLinkHistoryRecordType,
                SourceLinkId,
                version));

    private static DataRightsRequiredCompanionRequest
        IngestionCompanionRequest(DataRightsOperation operation) =>
        new(
            DataRightsRequiredCompanionContract.CurrentVersion,
            ScopeId,
            DataRightsCaseType.GuestRights,
            operation,
            PropertyId,
            Guid.NewGuid(),
            new DataRightsSubjectCoordinate(
                IngestionDataRightsCoordinates.Owner,
                IngestionDataRightsCoordinates
                    .ReservationSourceLinkRecordType,
                SourceLinkId,
                5),
            4);

    private static DataRightsRequiredCompanionRequest
        StaffCompanionRequest(DataRightsOperation operation) =>
        new(
            DataRightsRequiredCompanionContract.CurrentVersion,
            ScopeId,
            DataRightsCaseType.StaffRights,
            operation,
            PropertyId: null,
            Guid.NewGuid(),
            new DataRightsSubjectCoordinate(
                StaffDataRightsCoordinates.Owner,
                StaffDataRightsCoordinates.StaffMemberRecordType,
                StaffMemberId,
                7),
            4);

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

    private static DataRightsAnonymisationContributionRequest
        IngestionAnonymisationRequest(Guid workItemId, long version) =>
        AnonymisationRequest(workItemId, version) with
        {
            Coordinate = new DataRightsSubjectCoordinate(
                OperationsNotificationsDataRightsCoordinates.Owner,
                OperationsNotificationsDataRightsCoordinates
                    .IngestionSourceLinkHistoryRecordType,
                SourceLinkId,
                version)
        };

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

    private static NotificationHistoryReferenceRecord
        CreateProviderAttentionRecord(long sequence)
    {
        JsonElement payload = JsonSerializer.SerializeToElement(
            new ProviderAttentionNotificationPayload(
                PropertyId,
                Guid.NewGuid(),
                Guid.NewGuid(),
                ReservationId));
        return new NotificationHistoryReferenceRecord(
            Guid.NewGuid(),
            "staff-recipient",
            ReservationsModuleMetadata.Name,
            "provider-reservation-operation-needs-attention",
            1,
            "Provider change needs attention",
            "A provider reservation operation needs review.",
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

    private sealed class TestScopeContext(string? scopeId = null)
        : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId =>
            scopeId ?? OperationsNotificationsDataRightsTests.ScopeId;
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

        public Task<NotificationHistoryReferenceCloseBatchResult>
            CloseBatchAsync(
                NotificationHistoryReferenceCloseBatchRequest request,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                new NotificationHistoryReferenceCloseBatchResult(
                    NotificationHistoryReferenceCloseBatchStatus.Invalid,
                    null,
                    null));
    }
}
