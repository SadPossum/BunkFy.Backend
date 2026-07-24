namespace BunkFy.Modules.DataRights.Application.Handlers;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Application.Validation;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class DiscoverDataRightsSubjectsQueryHandler(
    IDataRightsCaseRepository cases,
    IEnumerable<IDataRightsSubjectDiscoveryContributor> contributors,
    IScopeContext scopeContext)
    : IQueryHandler<DiscoverDataRightsSubjectsQuery, DataRightsSubjectDiscoveryResponse>
{
    public async Task<Result<DataRightsSubjectDiscoveryResponse>> HandleAsync(
        DiscoverDataRightsSubjectsQuery query,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled || string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<DataRightsSubjectDiscoveryResponse>(
                DataRightsApplicationErrors.TenantRequired);
        }

        DataRightsCase? dataRightsCase = await cases.GetAsync(
            query.PropertyId,
            query.CaseId,
            cancellationToken).ConfigureAwait(false);
        if (dataRightsCase is null)
        {
            return Result.Failure<DataRightsSubjectDiscoveryResponse>(
                DataRightsApplicationErrors.CaseNotFound);
        }

        if (dataRightsCase.Status != DataRightsCaseState.Discovery)
        {
            return Result.Failure<DataRightsSubjectDiscoveryResponse>(
                DataRightsApplicationErrors.TransitionInvalid);
        }

        Result<DataRightsSubjectLookup> lookup = DataRightsSubjectLookupPolicy.Normalize(query.Lookup);
        if (lookup.IsFailure)
        {
            return Result.Failure<DataRightsSubjectDiscoveryResponse>(lookup.Error);
        }

        Result<IReadOnlyCollection<IDataRightsSubjectDiscoveryContributor>> contributorSet =
            DataRightsSubjectContributorSet.Order(contributors);
        if (contributorSet.IsFailure)
        {
            return Result.Failure<DataRightsSubjectDiscoveryResponse>(contributorSet.Error);
        }

        Dictionary<(string Owner, string RecordType, Guid RecordId), DataRightsSubjectCandidate>
            candidates = [];
        foreach (IDataRightsSubjectDiscoveryContributor contributor in contributorSet.Value)
        {
            int remaining = DataRightsSubjectDiscoveryLimits.MaxCandidates - candidates.Count;
            if (remaining == 0)
            {
                break;
            }

            DataRightsSubjectDiscoveryResult result = await contributor.DiscoverAsync(
                new DataRightsSubjectDiscoveryRequest(
                    scopeContext.ScopeId,
                    query.PropertyId,
                    lookup.Value,
                    remaining),
                cancellationToken).ConfigureAwait(false);
            if (result is null ||
                result.Status != DataRightsSubjectDiscoveryStatus.Succeeded)
            {
                return Result.Failure<DataRightsSubjectDiscoveryResponse>(
                    DataRightsApplicationErrors.DiscoveryScopeUnavailable);
            }

            if (result.Candidates is null || result.Candidates.Count > remaining)
            {
                return Result.Failure<DataRightsSubjectDiscoveryResponse>(
                    DataRightsApplicationErrors.SubjectCoordinateInvalid);
            }

            foreach (DataRightsSubjectCandidate candidate in result.Candidates)
            {
                if (!IsValid(candidate, contributor.OwnerKey))
                {
                    return Result.Failure<DataRightsSubjectDiscoveryResponse>(
                        DataRightsApplicationErrors.SubjectCoordinateInvalid);
                }

                (string Owner, string RecordType, Guid RecordId) key = (
                    candidate.Coordinate.OwnerKey.Trim().ToLowerInvariant(),
                    candidate.Coordinate.RecordType.Trim().ToLowerInvariant(),
                    candidate.Coordinate.RecordId);
                candidates.TryAdd(key, candidate);
                if (candidates.Count == DataRightsSubjectDiscoveryLimits.MaxCandidates)
                {
                    break;
                }
            }
        }

        DataRightsSubjectCandidate[] bounded = candidates.Values
            .OrderBy(candidate => candidate.Coordinate.OwnerKey, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Coordinate.RecordType, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Coordinate.RecordId)
            .Take(DataRightsSubjectDiscoveryLimits.MaxCandidates)
            .ToArray();
        return Result.Success(new DataRightsSubjectDiscoveryResponse(bounded));
    }

    private static bool IsValid(
        DataRightsSubjectCandidate? candidate,
        string contributorOwnerKey) =>
        candidate is not null &&
        candidate.Coordinate is not null &&
        !string.IsNullOrWhiteSpace(candidate.Coordinate.OwnerKey) &&
        !string.IsNullOrWhiteSpace(candidate.Coordinate.RecordType) &&
        string.Equals(
            candidate.Coordinate.OwnerKey.Trim(),
            contributorOwnerKey.Trim(),
            StringComparison.OrdinalIgnoreCase) &&
        candidate.Coordinate.OwnerKey.Trim().Length is > 0 and <=
            DataRightsSubjectDiscoveryLimits.OwnerKeyMaxLength &&
        candidate.Coordinate.RecordType.Trim().Length is > 0 and <=
            DataRightsSubjectDiscoveryLimits.RecordTypeMaxLength &&
        candidate.Coordinate.RecordId != Guid.Empty &&
        candidate.Coordinate.RecordVersion > 0 &&
        !string.IsNullOrWhiteSpace(candidate.DisplayName) &&
        candidate.DisplayName.Length <= DataRightsSubjectDiscoveryLimits.DisplayNameMaxLength &&
        (candidate.EmailHint is null ||
            candidate.EmailHint.Length <= DataRightsSubjectDiscoveryLimits.ContactHintMaxLength) &&
        (candidate.PhoneHint is null ||
            candidate.PhoneHint.Length <= DataRightsSubjectDiscoveryLimits.ContactHintMaxLength);
}
