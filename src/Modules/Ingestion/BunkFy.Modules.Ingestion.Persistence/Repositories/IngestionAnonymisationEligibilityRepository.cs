namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Properties.Contracts;
using Microsoft.EntityFrameworkCore;

internal sealed class IngestionAnonymisationEligibilityRepository(
    IngestionDbContext dbContext,
    IngestionDataRightsEvidenceGraphLoader graphLoader)
    : IIngestionAnonymisationEligibilityRepository
{
    public async Task<IngestionAnonymisationEligibilityLoadResult> LoadAsync(
        Guid propertyId,
        Guid sourceLinkId,
        CancellationToken cancellationToken)
    {
        ReservationSourceLink? sourceLink =
            await dbContext.ReservationSourceLinks
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    link =>
                        link.PropertyId == propertyId &&
                        link.Id == sourceLinkId &&
                        link.ReservationId != null,
                    cancellationToken)
                .ConfigureAwait(false);
        if (sourceLink is null)
        {
            return IngestionAnonymisationEligibilityLoadResult.NotFound();
        }

        AdapterConnection? connection = await dbContext.AdapterConnections
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item =>
                    item.PropertyId == propertyId &&
                    item.Id == sourceLink.ConnectionId,
                cancellationToken)
            .ConfigureAwait(false);
        if (connection is null)
        {
            return IngestionAnonymisationEligibilityLoadResult.Unavailable();
        }

        IngestionDataRightsEvidenceGraphLoadResult graphResult =
            await graphLoader.LoadAsync(
                sourceLink,
                cancellationToken).ConfigureAwait(false);
        if (graphResult.Status ==
            IngestionDataRightsEvidenceGraphLoadStatus.TooLarge)
        {
            return IngestionAnonymisationEligibilityLoadResult.TooLarge();
        }

        if (graphResult is not
            {
                Status: IngestionDataRightsEvidenceGraphLoadStatus.Succeeded,
                Graph: { } graph
            })
        {
            return IngestionAnonymisationEligibilityLoadResult.Unavailable();
        }

        IngestionPropertyProjection? property =
            await dbContext.PropertyProjections
                .AsNoTracking()
                .Include(item => item.GovernancePolicy)
                .ThenInclude(policy => policy!.Acknowledgements)
                .SingleOrDefaultAsync(
                    item => item.Id == propertyId,
                    cancellationToken)
                .ConfigureAwait(false);

        int activeLegalHoldCount = await dbContext.LegalHolds
            .AsNoTracking()
            .CountAsync(
                hold =>
                    hold.PropertyId == propertyId &&
                    hold.State == LegalHoldState.Active,
                cancellationToken)
            .ConfigureAwait(false);

        return IngestionAnonymisationEligibilityLoadResult.Found(
            new IngestionAnonymisationEligibilitySnapshot(
                new IngestionAnonymisationSourceLinkSnapshot(
                    sourceLink.Id,
                    sourceLink.PropertyId,
                    sourceLink.ConnectionId,
                    sourceLink.ReservationId,
                    sourceLink.State,
                    sourceLink.LastObservedReceiptId,
                    sourceLink.LastAppliedReceiptId,
                    sourceLink.ActiveProductOperationId,
                    sourceLink.DeferredReceiptId,
                    sourceLink.Version),
                new IngestionAnonymisationConnectionSnapshot(
                    connection.State,
                    connection.Version),
                property is null
                    ? null
                    : new IngestionAnonymisationPropertySnapshot(
                        property.IsKnown,
                        property.IsActive,
                        property.ProcessingStatus,
                        property.TopologySourceVersion,
                        property.PolicySourceVersion,
                        property.RetentionFenceVersion,
                        MapPolicy(property.GovernancePolicy)),
                activeLegalHoldCount,
                graph.Receipts.Select(receipt =>
                    new IngestionAnonymisationReceiptSnapshot(
                        receipt.Id,
                        receipt.State,
                        receipt.RawPayloadRetentionState,
                        receipt.RawPayloadVersion,
                        receipt.ActiveReprocessingAttemptId,
                        receipt.ReprocessingReservationExpiresAtUtc,
                        receipt.SourceReceiptId,
                        receipt.ReprocessingAttemptId)).ToArray(),
                graph.Proposals.Select(proposal =>
                    new IngestionAnonymisationProposalSnapshot(
                        proposal.Id,
                        proposal.ReceiptId,
                        proposal.State,
                        proposal.Version)).ToArray(),
                graph.Dispatches.Select(dispatch =>
                    new IngestionAnonymisationDispatchSnapshot(
                        dispatch.Id,
                        dispatch.ReceiptId,
                        dispatch.State,
                        dispatch.Version)).ToArray(),
                graph.Attempts.Select(attempt =>
                    new IngestionAnonymisationAttemptSnapshot(
                        attempt.Id,
                        attempt.SourceReceiptId,
                        attempt.State,
                        attempt.Version)).ToArray(),
                graph.Outputs.Select(output =>
                    new IngestionAnonymisationOutputSnapshot(
                        output.Id,
                        output.AttemptId,
                        output.OutputIndex,
                        output.Disposition,
                        output.ReceiptId)).ToArray()));
    }

    private static PropertyGovernancePolicyBinding? MapPolicy(
        IngestionPropertyPolicyBinding? policy) =>
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
}
