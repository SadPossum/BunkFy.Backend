namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Domain.Aggregates;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryDataRightsExportContributor(
    InventoryDbContext dbContext,
    IScopeContext scopeContext) : IDataRightsSubjectExportContributor
{
    public const int MaximumExportRecords = 1_000;
    public const string AmendmentDecisionRecordType =
        "allocation-amendment-decision";

    public string OwnerKey => InventoryDataRightsDiscoveryContributor.Owner;

    public IReadOnlyCollection<DataRightsCaseType> SupportedCaseTypes { get; } =
        [DataRightsCaseType.GuestRights];

    public DataRightsExportDescriptor Descriptor =>
        InventoryDataRightsExportSchema.Descriptor;

    public async Task<DataRightsSubjectExportResult> ExportAsync(
        DataRightsSubjectExportRequest request,
        IDataRightsExportSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sink);

        if (!this.IsValidScope(request.CaseType, request.TenantId, request.PropertyId))
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        DataRightsSubjectCoordinate? coordinate = request.Coordinate;
        if (coordinate is null ||
            !string.Equals(
                coordinate.OwnerKey,
                InventoryDataRightsDiscoveryContributor.Owner,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                coordinate.RecordType,
                InventoryDataRightsDiscoveryContributor.AllocationRecordType,
                StringComparison.OrdinalIgnoreCase) ||
            coordinate.RecordId == Guid.Empty ||
            coordinate.RecordVersion <= 0)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        Guid propertyId = request.PropertyId!.Value;
        if (!await this.IsKnownPropertyAsync(
                propertyId,
                cancellationToken).ConfigureAwait(false))
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        InventoryAllocation? allocationEntity =
            await dbContext.Allocations
                .AsNoTracking()
                .Include(item => item.Units)
                .SingleOrDefaultAsync(
                    item =>
                    item.PropertyId == propertyId &&
                    item.Id == coordinate.RecordId &&
                    !item.IsAnonymised,
                    cancellationToken)
                .ConfigureAwait(false);
        if (allocationEntity is null)
        {
            return DataRightsSubjectExportResult.NotFound();
        }

        if (allocationEntity.Version != coordinate.RecordVersion)
        {
            return DataRightsSubjectExportResult.Stale();
        }

        InventoryAllocationAmendmentDecisionDataRightsExport[] decisions =
            await dbContext.AllocationAmendmentDecisions
                .AsNoTracking()
                .Where(decision =>
                    decision.PropertyId == propertyId &&
                    decision.AllocationId == coordinate.RecordId)
                .OrderBy(decision => decision.DecidedAtUtc)
                .ThenBy(decision => decision.Id)
                .Select(decision =>
                    new InventoryAllocationAmendmentDecisionDataRightsExport(
                        decision.Id,
                        decision.AllocationId,
                        decision.ReservationId,
                        decision.PropertyId,
                        decision.RequestFingerprint,
                        decision.Confirmed,
                        decision.RejectionReason,
                        decision.AllocationVersion,
                        decision.DecidedAtUtc))
                .Take(MaximumExportRecords)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
        if (decisions.Length >= MaximumExportRecords)
        {
            return DataRightsSubjectExportResult.ScopeUnavailable();
        }

        InventoryAllocationDataRightsExport allocation = new(
            allocationEntity.Id,
            allocationEntity.ReservationId,
            allocationEntity.AllocationRequestId,
            allocationEntity.PropertyId,
            allocationEntity.Arrival,
            allocationEntity.Departure,
            allocationEntity.Status,
            allocationEntity.Rejection,
            allocationEntity.Version,
            allocationEntity.ReleaseRequestId,
            allocationEntity.CreatedAtUtc,
            allocationEntity.ReleasedAtUtc,
            allocationEntity.Units
                .OrderBy(unit => unit.InventoryUnitId)
                .Select(unit => unit.InventoryUnitId)
                .ToArray());
        await sink.WriteAsync(
            InventoryDataRightsExportSchema.CreateRecord(
                InventoryDataRightsDiscoveryContributor.AllocationRecordType,
                allocation.AllocationId,
                allocation.Version,
                allocation),
            cancellationToken).ConfigureAwait(false);
        int recordCount = 1;

        foreach (InventoryAllocationAmendmentDecisionDataRightsExport decision in
                 decisions)
        {
            await sink.WriteAsync(
                InventoryDataRightsExportSchema.CreateRecord(
                    AmendmentDecisionRecordType,
                    decision.AmendmentRequestId,
                    recordVersion: 1,
                    decision),
                cancellationToken).ConfigureAwait(false);
            recordCount = checked(recordCount + 1);
        }

        return DataRightsSubjectExportResult.Success(recordCount);
    }

    private Task<bool> IsKnownPropertyAsync(
        Guid propertyId,
        CancellationToken cancellationToken) =>
        dbContext.PropertyTopology
            .AsNoTracking()
            .AnyAsync(
                property =>
                    property.Id == propertyId &&
                    property.IsKnown,
                cancellationToken);

    private bool IsValidScope(
        DataRightsCaseType caseType,
        string tenantId,
        Guid? propertyId) =>
        caseType == DataRightsCaseType.GuestRights &&
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        string.Equals(
            scopeContext.ScopeId,
            tenantId?.Trim(),
            StringComparison.Ordinal) &&
        propertyId.HasValue &&
        propertyId.Value != Guid.Empty;
}
