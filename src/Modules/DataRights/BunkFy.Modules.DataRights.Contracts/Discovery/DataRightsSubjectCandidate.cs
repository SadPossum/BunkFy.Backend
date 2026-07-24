namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsSubjectCandidate(
    DataRightsSubjectCoordinate Coordinate,
    string DisplayName,
    string? EmailHint,
    string? PhoneHint);

public sealed record DataRightsSubjectDiscoveryRequest(
    string TenantId,
    Guid PropertyId,
    DataRightsSubjectLookup Lookup,
    int MaxCandidates);

public enum DataRightsSubjectDiscoveryStatus
{
    Unknown = 0,
    Succeeded = 1,
    ScopeUnavailable = 2
}

public sealed record DataRightsSubjectDiscoveryResult(
    DataRightsSubjectDiscoveryStatus Status,
    IReadOnlyCollection<DataRightsSubjectCandidate> Candidates)
{
    public static DataRightsSubjectDiscoveryResult Success(
        IReadOnlyCollection<DataRightsSubjectCandidate> candidates) =>
        new(DataRightsSubjectDiscoveryStatus.Succeeded, candidates);

    public static DataRightsSubjectDiscoveryResult ScopeUnavailable() =>
        new(DataRightsSubjectDiscoveryStatus.ScopeUnavailable, []);
}

public sealed record DataRightsSubjectDiscoveryResponse(
    IReadOnlyCollection<DataRightsSubjectCandidate> Candidates);
