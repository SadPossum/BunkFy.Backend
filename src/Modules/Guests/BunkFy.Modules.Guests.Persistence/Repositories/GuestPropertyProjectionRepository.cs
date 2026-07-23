namespace BunkFy.Modules.Guests.Persistence.Repositories;

using BunkFy.Modules.Guests.Application.Ports;
using Microsoft.EntityFrameworkCore;
using BunkFy.Modules.Properties.Contracts;

internal sealed class GuestPropertyProjectionRepository(GuestsDbContext dbContext)
    : IGuestPropertyProjectionRepository
{
    public async Task ApplyTopologyAsync(
        GuestPropertyTopologyWriteModel property,
        CancellationToken cancellationToken)
    {
        GuestPropertyProjection current = await this.GetOrCreateAsync(
            property.ScopeId,
            property.PropertyId,
            cancellationToken).ConfigureAwait(false);
        current.ApplyTopology(property.Name, property.Status, property.SourceVersion);
    }

    public async Task ApplyPolicyAsync(
        GuestPropertyPolicyWriteModel property,
        CancellationToken cancellationToken)
    {
        GuestPropertyProjection current = await this.GetOrCreateAsync(
            property.ScopeId,
            property.PropertyId,
            cancellationToken).ConfigureAwait(false);
        current.ApplyPolicy(property.ProcessingStatus, property.GovernancePolicy, property.SourceVersion);
    }

    public async Task<GuestPropertyPolicySnapshot?> GetPolicyAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        GuestPropertyProjection? property = await dbContext.PropertyProjections
            .AsNoTracking()
            .Include(item => item.GovernancePolicy)
            .ThenInclude(policy => policy!.Acknowledgements)
            .FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken)
            .ConfigureAwait(false);
        return property is null
            ? null
            : new GuestPropertyPolicySnapshot(
                property.IsKnown,
                property.Status == PropertyStatus.Active,
                property.ProcessingStatus,
                MapPolicy(property.GovernancePolicy));
    }

    private async Task<GuestPropertyProjection> GetOrCreateAsync(
        string scopeId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        GuestPropertyProjection? current = dbContext.PropertyProjections.Local.FirstOrDefault(
            item => item.ScopeId == scopeId && item.Id == propertyId) ??
            await dbContext.PropertyProjections.FirstOrDefaultAsync(
                item => item.Id == propertyId,
                cancellationToken).ConfigureAwait(false);
        if (current is not null)
        {
            return current;
        }

        current = new GuestPropertyProjection(
            scopeId,
            propertyId,
            null,
            PropertyStatus.Unknown,
            0);
        dbContext.PropertyProjections.Add(current);
        return current;
    }

    private static PropertyGovernancePolicyBinding? MapPolicy(GuestPropertyPolicyBinding? policy) =>
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
