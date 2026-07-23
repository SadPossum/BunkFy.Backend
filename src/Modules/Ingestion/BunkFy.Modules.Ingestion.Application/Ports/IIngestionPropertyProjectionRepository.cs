namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Properties.Contracts;

public interface IIngestionPropertyProjectionRepository
{
    Task ApplyTopologyAsync(IngestionPropertyTopologyWriteModel property, CancellationToken cancellationToken);
    Task ApplyPolicyAsync(IngestionPropertyPolicyWriteModel property, CancellationToken cancellationToken);
    Task ApplySnapshotAsync(IngestionPropertyProjectionWriteModel property, CancellationToken cancellationToken);
    Task<IngestionPropertyPolicySnapshot?> GetPolicyAsync(Guid propertyId, CancellationToken cancellationToken);
}

public sealed record IngestionPropertyTopologyWriteModel(
    string ScopeId,
    Guid PropertyId,
    string? Name,
    string? Code,
    bool IsActive,
    long SourceVersion);

public sealed record IngestionPropertyPolicyWriteModel(
    string ScopeId,
    Guid PropertyId,
    PropertyProcessingStatus ProcessingStatus,
    PropertyGovernancePolicyBinding? GovernancePolicy,
    long SourceVersion);

public sealed record IngestionPropertyProjectionWriteModel(
    string ScopeId,
    Guid PropertyId,
    string? Name,
    string? Code,
    bool IsActive,
    PropertyProcessingStatus ProcessingStatus,
    PropertyGovernancePolicyBinding? GovernancePolicy,
    long SourceVersion);

public sealed record IngestionPropertyPolicySnapshot(
    bool IsKnown,
    bool IsActive,
    PropertyProcessingStatus ProcessingStatus,
    PropertyGovernancePolicyBinding? GovernancePolicy);
