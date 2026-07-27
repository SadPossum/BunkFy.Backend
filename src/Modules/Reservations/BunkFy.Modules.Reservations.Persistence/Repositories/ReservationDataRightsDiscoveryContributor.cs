namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Contracts;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationDataRightsDiscoveryContributor(
    ReservationsDbContext dbContext,
    IScopeContext scopeContext) : IDataRightsSubjectDiscoveryContributor
{
    public const string Owner = ReservationsDataRightsCoordinates.Owner;
    public const string ReservationRecordType =
        ReservationsDataRightsCoordinates.ReservationRecordType;

    public string OwnerKey => Owner;

    public IReadOnlyCollection<DataRightsCaseType> SupportedCaseTypes { get; } =
        [DataRightsCaseType.GuestRights];

    public async Task<DataRightsSubjectDiscoveryResult> DiscoverAsync(
        DataRightsSubjectDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValidScope(request.CaseType, request.TenantId, request.PropertyId) ||
            request.MaxCandidates is <= 0 or > DataRightsSubjectDiscoveryLimits.MaxCandidates ||
            !HasOneStrongCoordinate(request.Lookup))
        {
            return DataRightsSubjectDiscoveryResult.ScopeUnavailable();
        }

        Guid propertyId = request.PropertyId!.Value;
        if (!await this.IsKnownPropertyAsync(propertyId, cancellationToken)
                .ConfigureAwait(false))
        {
            return DataRightsSubjectDiscoveryResult.ScopeUnavailable();
        }

        if (request.Lookup.DateOfBirth.HasValue)
        {
            return DataRightsSubjectDiscoveryResult.Success([]);
        }

        string? name = NormalizeSearch(request.Lookup.Name);
        IQueryable<ReservationSubjectMatch> query;
        if (request.Lookup.RecordId.HasValue)
        {
            Guid reservationId = request.Lookup.RecordId.Value;
            query = dbContext.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.PropertyId == propertyId &&
                    reservation.Id == reservationId &&
                    (name == null ||
                     reservation.PrimaryGuestNameSearch == name ||
                     reservation.PendingPrimaryGuestNameSearch == name))
                .OrderBy(reservation => reservation.Id)
                .Take(request.MaxCandidates)
                .Select(reservation => new ReservationSubjectMatch(
                    reservation.Id,
                    reservation.Version,
                    reservation.PrimaryGuestName,
                    reservation.Email,
                    reservation.Phone));
        }
        else
        {
            string? email = NormalizeSearch(request.Lookup.Email);
            string? phone = NormalizeSearch(request.Lookup.Phone);
            query = dbContext.Reservations
                .AsNoTracking()
                .Where(reservation =>
                    reservation.PropertyId == propertyId &&
                    ((email != null &&
                      reservation.EmailSearch == email &&
                      (name == null || reservation.PrimaryGuestNameSearch == name)) ||
                     (email != null &&
                      reservation.PendingEmailSearch == email &&
                      (name == null || reservation.PendingPrimaryGuestNameSearch == name)) ||
                     (phone != null &&
                      reservation.PhoneSearch == phone &&
                      (name == null || reservation.PrimaryGuestNameSearch == name)) ||
                     (phone != null &&
                      reservation.PendingPhoneSearch == phone &&
                      (name == null || reservation.PendingPrimaryGuestNameSearch == name))))
                .OrderBy(reservation => reservation.Id)
                .Take(request.MaxCandidates)
                .Select(reservation => new ReservationSubjectMatch(
                    reservation.Id,
                    reservation.Version,
                    ((email != null &&
                      reservation.PendingEmailSearch == email &&
                      (name == null || reservation.PendingPrimaryGuestNameSearch == name)) ||
                     (phone != null &&
                      reservation.PendingPhoneSearch == phone &&
                      (name == null || reservation.PendingPrimaryGuestNameSearch == name)))
                        ? reservation.PendingPrimaryGuestName!
                        : reservation.PrimaryGuestName,
                    ((email != null &&
                      reservation.PendingEmailSearch == email &&
                      (name == null || reservation.PendingPrimaryGuestNameSearch == name)) ||
                     (phone != null &&
                      reservation.PendingPhoneSearch == phone &&
                      (name == null || reservation.PendingPrimaryGuestNameSearch == name)))
                        ? reservation.PendingEmail
                        : reservation.Email,
                    ((email != null &&
                      reservation.PendingEmailSearch == email &&
                      (name == null || reservation.PendingPrimaryGuestNameSearch == name)) ||
                     (phone != null &&
                      reservation.PendingPhoneSearch == phone &&
                      (name == null || reservation.PendingPrimaryGuestNameSearch == name)))
                        ? reservation.PendingPhone
                        : reservation.Phone));
        }

        ReservationSubjectMatch[] matches = await query
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        DataRightsSubjectCandidate[] candidates = matches
            .Select(match => new DataRightsSubjectCandidate(
                new DataRightsSubjectCoordinate(
                    Owner,
                    ReservationRecordType,
                    match.Id,
                    match.Version),
                match.DisplayName,
                MaskEmail(match.Email),
                MaskPhone(match.Phone)))
            .ToArray();
        return DataRightsSubjectDiscoveryResult.Success(candidates);
    }

    public async Task<DataRightsSubjectSelectionValidation> ValidateSelectionAsync(
        DataRightsSubjectSelectionRequest request,
        CancellationToken cancellationToken)
    {
        if (!this.IsValidScope(request.CaseType, request.TenantId, request.PropertyId) ||
            !string.Equals(request.Coordinate.OwnerKey, Owner, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                request.Coordinate.RecordType,
                ReservationRecordType,
                StringComparison.OrdinalIgnoreCase) ||
            request.Coordinate.RecordId == Guid.Empty ||
            request.Coordinate.RecordVersion <= 0)
        {
            return DataRightsSubjectSelectionValidation.NotFound();
        }

        Guid propertyId = request.PropertyId!.Value;
        if (!await this.IsKnownPropertyAsync(propertyId, cancellationToken)
                .ConfigureAwait(false))
        {
            return DataRightsSubjectSelectionValidation.ScopeUnavailable();
        }

        long? version = await dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.PropertyId == propertyId &&
                reservation.Id == request.Coordinate.RecordId)
            .Select(reservation => (long?)reservation.Version)
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

        return DataRightsSubjectSelectionValidation.Valid(new DataRightsSubjectCoordinate(
            Owner,
            ReservationRecordType,
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

    private bool IsValidScope(
        DataRightsCaseType caseType,
        string tenantId,
        Guid? propertyId) =>
        caseType == DataRightsCaseType.GuestRights &&
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        string.Equals(scopeContext.ScopeId, tenantId?.Trim(), StringComparison.Ordinal) &&
        propertyId.HasValue &&
        propertyId.Value != Guid.Empty;

    private static bool HasOneStrongCoordinate(DataRightsSubjectLookup? lookup)
    {
        if (lookup is null ||
            lookup.RecordId == Guid.Empty ||
            !string.IsNullOrWhiteSpace(lookup.AccountSubjectId))
        {
            return false;
        }

        int strongCoordinates =
            (lookup.RecordId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(lookup.Email) ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(lookup.Phone) ? 1 : 0);
        return strongCoordinates == 1;
    }

    private static string? NormalizeSearch(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

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

    private sealed record ReservationSubjectMatch(
        Guid Id,
        long Version,
        string DisplayName,
        string? Email,
        string? Phone);
}
