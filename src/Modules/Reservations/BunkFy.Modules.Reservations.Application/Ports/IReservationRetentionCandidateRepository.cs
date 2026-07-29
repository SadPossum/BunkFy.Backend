namespace BunkFy.Modules.Reservations.Application.Ports;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;

internal interface IReservationRetentionCandidateRepository
{
    Task<ReservationRetentionScanPage> ScanAsync(
        long afterProjectionOrdinal,
        int limit,
        CancellationToken cancellationToken);

    Task<ReservationRetentionCandidateSnapshot?> LoadAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken);
}

internal sealed record ReservationRetentionScanPage(
    IReadOnlyList<ReservationRetentionCandidateSnapshot> Candidates,
    bool ReachedEnd);

internal sealed record ReservationRetentionCandidateSnapshot(
    Guid ReservationId,
    Guid PropertyId,
    long ReservationVersion,
    long DetailsRevision,
    long ProjectionOrdinal,
    ReservationState Status,
    bool HasPendingAllocationAmendment,
    bool IsAnonymised,
    DateTimeOffset? TerminalAtUtc,
    int ActiveHoldCount,
    DateTimeOffset? EarliestHoldPlacedAtUtc,
    int? ProcessingRestrictionContractVersion,
    ReservationRetentionPropertySnapshot? Property);

internal sealed record ReservationRetentionPropertySnapshot(
    bool IsKnown,
    bool IsActive,
    PropertyProcessingStatus ProcessingStatus,
    long TopologySourceVersion,
    long PolicySourceVersion,
    PropertyGovernancePolicyBinding? GovernancePolicy);
