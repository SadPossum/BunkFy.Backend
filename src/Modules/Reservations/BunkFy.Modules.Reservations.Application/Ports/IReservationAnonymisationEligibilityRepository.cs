namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

public interface IReservationAnonymisationEligibilityRepository
{
    Task<ReservationAnonymisationEligibilitySnapshot?> LoadAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken);
}

public sealed record ReservationAnonymisationEligibilitySnapshot(
    long ReservationVersion,
    long DetailsRevision,
    ReservationState Status,
    bool HasPendingAllocationAmendment,
    ReservationSource Source,
    bool HasDirectSourceReference,
    int ActiveHoldCount,
    int? ProcessingRestrictionContractVersion,
    ReservationAnonymisationPropertySnapshot? Property);

public sealed record ReservationAnonymisationPropertySnapshot(
    bool IsKnown,
    bool IsActive,
    PropertyProcessingStatus ProcessingStatus,
    long TopologySourceVersion,
    long PolicySourceVersion,
    PropertyGovernancePolicyBinding? GovernancePolicy);
