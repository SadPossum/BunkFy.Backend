namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class InventoryDataRightsDiscoveryContributor(
    InventoryDbContext dbContext,
    IScopeContext scopeContext) : IDataRightsSubjectDiscoveryContributor
{
    public const string Owner = InventoryDataRightsCoordinates.Owner;
    public const string AllocationRecordType =
        InventoryDataRightsCoordinates.AllocationRecordType;

    public string OwnerKey => Owner;

    public async Task<DataRightsSubjectDiscoveryResult> DiscoverAsync(
        DataRightsSubjectDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValidScope(request.TenantId, request.PropertyId) ||
            request.MaxCandidates is <= 0 or >
                DataRightsSubjectDiscoveryLimits.MaxCandidates ||
            !HasExactReservationId(request.Lookup))
        {
            return DataRightsSubjectDiscoveryResult.ScopeUnavailable();
        }

        if (!await this.IsKnownPropertyAsync(
                request.PropertyId,
                cancellationToken).ConfigureAwait(false))
        {
            return DataRightsSubjectDiscoveryResult.ScopeUnavailable();
        }

        Guid reservationId = request.Lookup.RecordId!.Value;
        InventoryAllocationCandidate[] allocations = await dbContext.Allocations
            .AsNoTracking()
            .Where(allocation =>
                allocation.PropertyId == request.PropertyId &&
                allocation.ReservationId == reservationId &&
                !allocation.IsAnonymised)
            .OrderBy(allocation => allocation.Id)
            .Take(request.MaxCandidates)
            .Select(allocation => new InventoryAllocationCandidate(
                allocation.Id,
                allocation.Version,
                allocation.Arrival,
                allocation.Departure))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        DataRightsSubjectCandidate[] candidates = allocations
            .Select(allocation => new DataRightsSubjectCandidate(
                new DataRightsSubjectCoordinate(
                    Owner,
                    AllocationRecordType,
                    allocation.Id,
                    allocation.Version),
                "Inventory allocation - " +
                $"{allocation.Arrival:yyyy-MM-dd} to " +
                $"{allocation.Departure:yyyy-MM-dd}",
                EmailHint: null,
                PhoneHint: null))
            .ToArray();
        return DataRightsSubjectDiscoveryResult.Success(candidates);
    }

    public async Task<DataRightsSubjectSelectionValidation>
        ValidateSelectionAsync(
            DataRightsSubjectSelectionRequest request,
            CancellationToken cancellationToken)
    {
        if (!this.IsValidScope(request.TenantId, request.PropertyId) ||
            !string.Equals(
                request.Coordinate.OwnerKey,
                Owner,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                request.Coordinate.RecordType,
                AllocationRecordType,
                StringComparison.OrdinalIgnoreCase) ||
            request.Coordinate.RecordId == Guid.Empty ||
            request.Coordinate.RecordVersion <= 0)
        {
            return DataRightsSubjectSelectionValidation.NotFound();
        }

        if (!await this.IsKnownPropertyAsync(
                request.PropertyId,
                cancellationToken).ConfigureAwait(false))
        {
            return DataRightsSubjectSelectionValidation.ScopeUnavailable();
        }

        long? version = await dbContext.Allocations
            .AsNoTracking()
            .Where(allocation =>
                allocation.PropertyId == request.PropertyId &&
                allocation.Id == request.Coordinate.RecordId &&
                !allocation.IsAnonymised)
            .Select(allocation => (long?)allocation.Version)
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!version.HasValue)
        {
            return DataRightsSubjectSelectionValidation.NotFound();
        }

        if (version.Value != request.Coordinate.RecordVersion)
        {
            return DataRightsSubjectSelectionValidation.Stale();
        }

        return DataRightsSubjectSelectionValidation.Valid(
            new DataRightsSubjectCoordinate(
                Owner,
                AllocationRecordType,
                request.Coordinate.RecordId,
                version.Value));
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

    private bool IsValidScope(string tenantId, Guid propertyId) =>
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        string.Equals(
            scopeContext.ScopeId,
            tenantId?.Trim(),
            StringComparison.Ordinal) &&
        propertyId != Guid.Empty;

    private static bool HasExactReservationId(
        DataRightsSubjectLookup? lookup) =>
        lookup is
        {
            RecordId: { } recordId,
            Email: null,
            Phone: null,
            Name: null,
            DateOfBirth: null
        } &&
        recordId != Guid.Empty;

    private sealed record InventoryAllocationCandidate(
        Guid Id,
        long Version,
        DateOnly Arrival,
        DateOnly Departure);
}
