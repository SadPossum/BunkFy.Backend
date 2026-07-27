namespace BunkFy.Modules.Staff.Persistence.Repositories;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using Gma.Framework.Scoping;
using Microsoft.EntityFrameworkCore;

internal sealed class StaffDataRightsDiscoveryContributor(
    StaffDbContext dbContext,
    IScopeContext scopeContext) : IDataRightsSubjectDiscoveryContributor
{
    public const string Owner = StaffModuleMetadata.Name;
    public const string ProfileRecordType = "staff-member";

    public string OwnerKey => Owner;

    public IReadOnlyCollection<DataRightsCaseType> SupportedCaseTypes { get; } =
        [DataRightsCaseType.StaffRights];

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

        IQueryable<StaffMember> query = dbContext.StaffMembers.AsNoTracking();
        if (request.Lookup.RecordId.HasValue)
        {
            Guid recordId = request.Lookup.RecordId.Value;
            query = query.Where(member => member.Id == recordId);
        }
        else
        {
            string accountSubjectId = request.Lookup.AccountSubjectId!.Trim();
            query = query.Where(member => member.AuthSubjectId == accountSubjectId);
        }

        StaffMember[] members = await query
            .OrderBy(member => member.Id)
            .Take(request.MaxCandidates)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        DataRightsSubjectCandidate[] candidates = members
            .Select(member => new DataRightsSubjectCandidate(
                new DataRightsSubjectCoordinate(
                    Owner,
                    ProfileRecordType,
                    member.Id,
                    member.Version),
                member.DisplayName,
                MaskEmail(member.WorkEmail),
                MaskPhone(member.WorkPhone)))
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
                ProfileRecordType,
                StringComparison.OrdinalIgnoreCase) ||
            request.Coordinate.RecordId == Guid.Empty ||
            request.Coordinate.RecordVersion <= 0)
        {
            return DataRightsSubjectSelectionValidation.NotFound();
        }

        StaffVersion? member = await dbContext.StaffMembers
            .AsNoTracking()
            .Where(candidate => candidate.Id == request.Coordinate.RecordId)
            .Select(candidate => new StaffVersion(candidate.Version))
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (member is null)
        {
            return DataRightsSubjectSelectionValidation.NotFound();
        }

        if (member.Version != request.Coordinate.RecordVersion)
        {
            return DataRightsSubjectSelectionValidation.Stale();
        }

        return DataRightsSubjectSelectionValidation.Valid(new DataRightsSubjectCoordinate(
            Owner,
            ProfileRecordType,
            request.Coordinate.RecordId,
            member.Version));
    }

    private bool IsValidScope(
        DataRightsCaseType caseType,
        string tenantId,
        Guid? propertyId) =>
        caseType == DataRightsCaseType.StaffRights &&
        scopeContext.IsEnabled &&
        !string.IsNullOrWhiteSpace(scopeContext.ScopeId) &&
        string.Equals(scopeContext.ScopeId, tenantId?.Trim(), StringComparison.Ordinal) &&
        !propertyId.HasValue;

    private static bool HasOneStrongCoordinate(DataRightsSubjectLookup? lookup)
    {
        if (lookup is null ||
            lookup.RecordId == Guid.Empty ||
            !string.IsNullOrWhiteSpace(lookup.Email) ||
            !string.IsNullOrWhiteSpace(lookup.Phone) ||
            !string.IsNullOrWhiteSpace(lookup.Name) ||
            lookup.DateOfBirth.HasValue)
        {
            return false;
        }

        int strongCoordinates =
            (lookup.RecordId.HasValue ? 1 : 0) +
            (!string.IsNullOrWhiteSpace(lookup.AccountSubjectId) ? 1 : 0);
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

    private sealed record StaffVersion(long Version);
}
