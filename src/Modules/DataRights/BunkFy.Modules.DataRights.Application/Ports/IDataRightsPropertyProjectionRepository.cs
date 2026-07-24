namespace BunkFy.Modules.DataRights.Application.Ports;

using BunkFy.Modules.Properties.Contracts;

public interface IDataRightsPropertyProjectionRepository
{
    Task ApplyTopologyAsync(
        DataRightsPropertyTopologyWriteModel property,
        CancellationToken cancellationToken);

    Task ApplyPolicyAsync(
        DataRightsPropertyPolicyWriteModel property,
        CancellationToken cancellationToken);

    Task<DataRightsPropertyPolicySnapshot?> GetPolicyAsync(
        Guid propertyId,
        CancellationToken cancellationToken);
}

public sealed record DataRightsPropertyTopologyWriteModel(
    string ScopeId,
    Guid PropertyId,
    string Name,
    PropertyStatus Status,
    long SourceVersion);

public sealed record DataRightsPropertyPolicyWriteModel(
    string ScopeId,
    Guid PropertyId,
    PropertyProcessingStatus ProcessingStatus,
    PropertyGovernancePolicyBinding? GovernancePolicy,
    long SourceVersion);

public sealed record DataRightsPropertyPolicySnapshot(
    bool IsKnown,
    bool IsActive,
    PropertyProcessingStatus ProcessingStatus,
    PropertyGovernancePolicyBinding? GovernancePolicy,
    long PolicySourceVersion);
