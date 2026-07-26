namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionDataRightsDiscoveryContributor(
    IngestionDbContext dbContext,
    IScopeContext scopeContext) : IDataRightsSubjectDiscoveryContributor
{
    public const string Owner = IngestionDataRightsCoordinates.Owner;
    public const string SourceLinkRecordType =
        IngestionDataRightsCoordinates.ReservationSourceLinkRecordType;

    public string OwnerKey => Owner;

    public async Task<DataRightsSubjectDiscoveryResult> DiscoverAsync(
        DataRightsSubjectDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValidScope(request.TenantId, request.PropertyId) ||
            request.MaxCandidates is <= 0 or > DataRightsSubjectDiscoveryLimits.MaxCandidates ||
            !HasExactReservationId(request.Lookup))
        {
            return DataRightsSubjectDiscoveryResult.ScopeUnavailable();
        }

        if (!await this.IsKnownPropertyAsync(request.PropertyId, cancellationToken)
                .ConfigureAwait(false))
        {
            return DataRightsSubjectDiscoveryResult.ScopeUnavailable();
        }

        Guid reservationId = request.Lookup.RecordId!.Value;
        SourceLinkCandidate[] links = await dbContext.ReservationSourceLinks
            .AsNoTracking()
            .Where(link =>
                link.PropertyId == request.PropertyId &&
                link.ReservationId == reservationId &&
                link.State != ReservationSourceLinkState.Anonymised &&
                !dbContext.AnonymisationTombstones.Any(tombstone =>
                    tombstone.Id == link.Id))
            .OrderBy(link => link.Id)
            .Take(request.MaxCandidates)
            .Select(link => new SourceLinkCandidate(
                link.Id,
                link.Version,
                link.SourceSystem))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        DataRightsSubjectCandidate[] candidates = links
            .Select(link => new DataRightsSubjectCandidate(
                new DataRightsSubjectCoordinate(
                    Owner,
                    SourceLinkRecordType,
                    link.Id,
                    link.Version),
                $"{link.SourceSystem} reservation evidence",
                EmailHint: null,
                PhoneHint: null))
            .ToArray();
        return DataRightsSubjectDiscoveryResult.Success(candidates);
    }

    public async Task<DataRightsSubjectSelectionValidation> ValidateSelectionAsync(
        DataRightsSubjectSelectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValidScope(request.TenantId, request.PropertyId) ||
            !string.Equals(request.Coordinate.OwnerKey, Owner, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                request.Coordinate.RecordType,
                SourceLinkRecordType,
                StringComparison.OrdinalIgnoreCase) ||
            request.Coordinate.RecordId == Guid.Empty ||
            request.Coordinate.RecordVersion <= 0)
        {
            return DataRightsSubjectSelectionValidation.NotFound();
        }

        if (!await this.IsKnownPropertyAsync(request.PropertyId, cancellationToken)
                .ConfigureAwait(false))
        {
            return DataRightsSubjectSelectionValidation.ScopeUnavailable();
        }

        long? version = await dbContext.ReservationSourceLinks
            .AsNoTracking()
            .Where(link =>
                link.PropertyId == request.PropertyId &&
                link.Id == request.Coordinate.RecordId &&
                link.ReservationId != null &&
                link.State != ReservationSourceLinkState.Anonymised &&
                !dbContext.AnonymisationTombstones.Any(tombstone =>
                    tombstone.Id == link.Id))
            .Select(link => (long?)link.Version)
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
                SourceLinkRecordType,
                request.Coordinate.RecordId,
                version.Value));
    }

    private Task<bool> IsKnownPropertyAsync(
        Guid propertyId,
        CancellationToken cancellationToken) =>
        dbContext.PropertyProjections
            .AsNoTracking()
            .AnyAsync(
                property => property.Id == propertyId && property.IsKnown,
                cancellationToken);

    private bool IsValidScope(string tenantId, Guid propertyId) =>
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        string.Equals(scopeContext.ScopeId, tenantId?.Trim(), StringComparison.Ordinal) &&
        propertyId != Guid.Empty;

    private static bool HasExactReservationId(DataRightsSubjectLookup? lookup) =>
        lookup is
        {
            RecordId: { } recordId,
            Email: null,
            Phone: null,
            Name: null,
            DateOfBirth: null
        } &&
        recordId != Guid.Empty;

    private sealed record SourceLinkCandidate(
        Guid Id,
        long Version,
        string SourceSystem);
}
