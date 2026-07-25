namespace BunkFy.Modules.Guests.Application.Ports;

using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Properties.Contracts;

public interface IGuestAnonymisationEligibilityRepository
{
    Task<GuestAnonymisationEligibilitySnapshot?> LoadAsync(
        Guid guestId,
        CancellationToken cancellationToken);
}

public sealed record GuestAnonymisationEligibilitySnapshot(
    long GuestVersion,
    GuestProfileState GuestState,
    Guid OriginPropertyId,
    IReadOnlyCollection<GuestAnonymisationStaySnapshot> Stays,
    IReadOnlyCollection<Guid> ActiveHoldPropertyIds,
    IReadOnlyCollection<GuestAnonymisationPropertySnapshot> Properties);

public sealed record GuestAnonymisationStaySnapshot(
    Guid PropertyId,
    Guid ReservationId,
    GuestStayRole Role,
    GuestStayStatus Status,
    bool IsCurrentParticipant,
    long ReservationVersion,
    int ProjectionContractVersion);

public sealed record GuestAnonymisationPropertySnapshot(
    Guid PropertyId,
    bool IsKnown,
    bool IsActive,
    PropertyProcessingStatus ProcessingStatus,
    long TopologySourceVersion,
    long PolicySourceVersion,
    PropertyGovernancePolicyBinding? GovernancePolicy);
