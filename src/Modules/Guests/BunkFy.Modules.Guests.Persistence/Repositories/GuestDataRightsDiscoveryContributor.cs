namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class GuestDataRightsDiscoveryContributor(
    GuestsDbContext dbContext,
    IScopeContext scopeContext) : IDataRightsSubjectDiscoveryContributor
{
    public const string Owner = GuestsDataRightsCoordinates.Owner;
    public const string ProfileRecordType = GuestsDataRightsCoordinates.GuestProfileRecordType;

    public string OwnerKey => Owner;

    public async Task<DataRightsSubjectDiscoveryResult> DiscoverAsync(
        DataRightsSubjectDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValidScope(request.TenantId, request.PropertyId) ||
            request.MaxCandidates is <= 0 or > DataRightsSubjectDiscoveryLimits.MaxCandidates ||
            !HasOneStrongCoordinate(request.Lookup))
        {
            return DataRightsSubjectDiscoveryResult.ScopeUnavailable();
        }

        if (!await this.IsKnownPropertyAsync(request.PropertyId, cancellationToken).ConfigureAwait(false))
        {
            return DataRightsSubjectDiscoveryResult.ScopeUnavailable();
        }

        IQueryable<GuestProfile> query = this.VisibleAtProperty(request.PropertyId);
        if (request.Lookup.RecordId.HasValue)
        {
            Guid recordId = request.Lookup.RecordId.Value;
            query = query.Where(profile => profile.Id == recordId);
        }
        else if (!string.IsNullOrWhiteSpace(request.Lookup.Email))
        {
            string email = request.Lookup.Email.Trim().ToUpperInvariant();
            query = query.Where(profile => profile.EmailSearch == email);
        }
        else
        {
            string phone = request.Lookup.Phone!.Trim().ToUpperInvariant();
            query = query.Where(profile => profile.PhoneSearch == phone);
        }

        if (!string.IsNullOrWhiteSpace(request.Lookup.Name))
        {
            string name = request.Lookup.Name.Trim().ToUpperInvariant();
            query = query.Where(profile =>
                profile.DisplayNameSearch == name ||
                profile.LegalNameSearch == name);
        }

        if (request.Lookup.DateOfBirth.HasValue)
        {
            DateOnly dateOfBirth = request.Lookup.DateOfBirth.Value;
            query = query.Where(profile => profile.DateOfBirth == dateOfBirth);
        }

        GuestProfile[] profiles = await query
            .OrderBy(profile => profile.Id)
            .Take(request.MaxCandidates)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        DataRightsSubjectCandidate[] candidates = profiles
            .Select(profile => new DataRightsSubjectCandidate(
                new DataRightsSubjectCoordinate(
                    Owner,
                    ProfileRecordType,
                    profile.Id,
                    profile.Version),
                profile.DisplayName,
                MaskEmail(profile.Email),
                MaskPhone(profile.Phone)))
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
                ProfileRecordType,
                StringComparison.OrdinalIgnoreCase) ||
            request.Coordinate.RecordId == Guid.Empty ||
            request.Coordinate.RecordVersion <= 0)
        {
            return DataRightsSubjectSelectionValidation.NotFound();
        }

        if (!await this.IsKnownPropertyAsync(request.PropertyId, cancellationToken).ConfigureAwait(false))
        {
            return DataRightsSubjectSelectionValidation.ScopeUnavailable();
        }

        GuestVersion? profile = await this.VisibleAtProperty(request.PropertyId)
            .Where(candidate => candidate.Id == request.Coordinate.RecordId)
            .Select(candidate => new GuestVersion(candidate.Version))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return DataRightsSubjectSelectionValidation.NotFound();
        }

        if (profile.Version != request.Coordinate.RecordVersion)
        {
            return DataRightsSubjectSelectionValidation.Stale();
        }

        return DataRightsSubjectSelectionValidation.Valid(new DataRightsSubjectCoordinate(
            Owner,
            ProfileRecordType,
            request.Coordinate.RecordId,
            profile.Version));
    }

    private IQueryable<GuestProfile> VisibleAtProperty(Guid propertyId) =>
        dbContext.GuestProfiles
            .AsNoTracking()
            .Where(profile =>
                profile.OriginPropertyId == propertyId ||
                dbContext.StayHistory.Any(stay =>
                    stay.GuestId == profile.Id &&
                    stay.PropertyId == propertyId));

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

    private static bool HasOneStrongCoordinate(DataRightsSubjectLookup? lookup)
    {
        if (lookup is null || lookup.RecordId == Guid.Empty)
        {
            return false;
        }

        int strongCoordinates =
            (lookup.RecordId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(lookup.Email) ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(lookup.Phone) ? 1 : 0);
        return strongCoordinates == 1;
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        int separator = email.IndexOf('@');
        return separator <= 0
            ? "***"
            : $"{email[0]}***{email[separator..]}";
    }

    private static string? MaskPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        string digits = new(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 4 ? $"***{digits[^4..]}" : "***";
    }

    private sealed record GuestVersion(long Version);
}
