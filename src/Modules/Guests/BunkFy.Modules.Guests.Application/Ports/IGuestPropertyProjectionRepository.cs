namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Properties.Contracts;

public interface IGuestPropertyProjectionRepository
{
    Task ApplyTopologyAsync(GuestPropertyTopologyWriteModel property, CancellationToken cancellationToken);
    Task ApplyPolicyAsync(GuestPropertyPolicyWriteModel property, CancellationToken cancellationToken);
    Task<GuestPropertyPolicySnapshot?> GetPolicyAsync(Guid propertyId, CancellationToken cancellationToken);
}

public sealed record GuestPropertyTopologyWriteModel(
    string ScopeId,
    Guid PropertyId,
    string Name,
    PropertyStatus Status,
    long SourceVersion);

public sealed record GuestPropertyPolicyWriteModel(
    string ScopeId,
    Guid PropertyId,
    PropertyProcessingStatus ProcessingStatus,
    PropertyGovernancePolicyBinding? GovernancePolicy,
    long SourceVersion);

public sealed record GuestPropertyPolicySnapshot(
    bool IsKnown,
    bool IsActive,
    PropertyProcessingStatus ProcessingStatus,
    PropertyGovernancePolicyBinding? GovernancePolicy);
