namespace BunkFy.Modules.Reservations.Persistence.Repositories;

using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.Models;
using Microsoft.EntityFrameworkCore;

internal sealed class ReservationAnonymisationEligibilityRepository(
    ReservationsDbContext dbContext)
    : IReservationAnonymisationEligibilityRepository
{
    public async Task<ReservationAnonymisationEligibilitySnapshot?> LoadAsync(
        Guid propertyId,
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        ReservationEligibilityRow? reservation =
            await dbContext.Reservations.AsNoTracking()
                .Where(item =>
                    item.Id == reservationId &&
                    item.PropertyId == propertyId)
                .Select(item => new ReservationEligibilityRow(
                    item.Version,
                    item.DetailsRevision,
                    item.Status,
                    item.PendingAllocationAmendmentId != null,
                    item.Source,
                    item.SourceReference != null &&
                        item.SourceReference != string.Empty,
                    item.IsAnonymised,
                    dbContext.DataHolds.Count(hold =>
                        hold.PropertyId == propertyId &&
                        hold.ReservationId == reservationId &&
                        hold.State == ReservationDataHoldState.Active),
                    dbContext.ProcessingRestrictionProjections
                        .Where(projection =>
                            projection.PropertyId == propertyId &&
                            projection.ReservationId == reservationId)
                        .Select(projection => (int?)projection.ContractVersion)
                        .SingleOrDefault()))
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        if (reservation is null)
        {
            return null;
        }

        ReservationPropertyProjection? property =
            await dbContext.PropertyProjections.AsNoTracking()
                .Include(item => item.GovernancePolicy)
                .ThenInclude(policy => policy!.Acknowledgements)
                .SingleOrDefaultAsync(
                    item => item.Id == propertyId,
                    cancellationToken).ConfigureAwait(false);

        return new(
            reservation.ReservationVersion,
            reservation.DetailsRevision,
            reservation.Status,
            reservation.HasPendingAllocationAmendment,
            reservation.Source,
            reservation.HasDirectSourceReference,
            reservation.ActiveHoldCount,
            reservation.ProcessingRestrictionContractVersion,
            property is null
                ? null
                : new ReservationAnonymisationPropertySnapshot(
                    property.IsKnown,
                    property.IsActive,
                    property.ProcessingStatus,
                    property.TopologySourceVersion,
                    property.PolicySourceVersion,
                    MapPolicy(property.GovernancePolicy)),
            reservation.IsAnonymised);
    }

    private static PropertyGovernancePolicyBinding? MapPolicy(
        ReservationPropertyPolicyBinding? policy) =>
        policy is null
            ? null
            : new PropertyGovernancePolicyBinding(
                policy.OperatingCountryCode,
                policy.PolicyId,
                policy.PolicyVersion,
                policy.DataRegionId,
                policy.TransferProfileId,
                policy.RetentionPolicyId,
                policy.RetentionPolicyVersion,
                policy.ContentSha256,
                policy.PolicyEffectiveAtUtc,
                policy.PolicyExpiresAtUtc,
                policy.ActivatedAtUtc,
                policy.Acknowledgements.Select(acknowledgement =>
                    new PropertyGovernanceAcknowledgement(
                        acknowledgement.AcknowledgementId,
                        acknowledgement.AcknowledgementVersion)).ToArray());

    private sealed record ReservationEligibilityRow(
        long ReservationVersion,
        long DetailsRevision,
        ReservationState Status,
        bool HasPendingAllocationAmendment,
        ReservationSource Source,
        bool HasDirectSourceReference,
        bool IsAnonymised,
        int ActiveHoldCount,
        int? ProcessingRestrictionContractVersion);
}
