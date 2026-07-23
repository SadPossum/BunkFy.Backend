namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Properties.Contracts;

public interface IReservationPropertyPolicyRepository
{
    Task ApplyPolicyAsync(
        ReservationPropertyPolicyWriteModel property,
        CancellationToken cancellationToken);

    Task<ReservationPropertyPolicySnapshot?> GetPolicyAsync(
        Guid propertyId,
        CancellationToken cancellationToken);
}

public sealed record ReservationPropertyPolicyWriteModel(
    string ScopeId,
    Guid PropertyId,
    PropertyProcessingStatus ProcessingStatus,
    PropertyGovernancePolicyBinding? GovernancePolicy,
    long SourceVersion);

public sealed record ReservationPropertyPolicySnapshot(
    bool IsKnown,
    bool IsActive,
    PropertyProcessingStatus ProcessingStatus,
    PropertyGovernancePolicyBinding? GovernancePolicy);
