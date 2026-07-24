namespace BunkFy.Modules.DataRights.Contracts;

public interface IDataRightsSubjectDiscoveryContributor
{
    string OwnerKey { get; }

    Task<DataRightsSubjectDiscoveryResult> DiscoverAsync(
        DataRightsSubjectDiscoveryRequest request,
        CancellationToken cancellationToken);

    Task<DataRightsSubjectSelectionValidation> ValidateSelectionAsync(
        DataRightsSubjectSelectionRequest request,
        CancellationToken cancellationToken);
}
