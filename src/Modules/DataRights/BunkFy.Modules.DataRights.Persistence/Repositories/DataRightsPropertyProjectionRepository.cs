namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsPropertyProjectionRepository(DataRightsDbContext dbContext)
    : IDataRightsPropertyProjectionRepository
{
    public async Task ApplyTopologyAsync(
        DataRightsPropertyTopologyWriteModel property,
        CancellationToken cancellationToken)
    {
        DataRightsPropertyProjection current = await this.GetOrCreateAsync(
            property.ScopeId,
            property.PropertyId,
            cancellationToken).ConfigureAwait(false);
        current.ApplyTopology(property.Name, property.Status, property.SourceVersion);
    }

    public async Task ApplyPolicyAsync(
        DataRightsPropertyPolicyWriteModel property,
        CancellationToken cancellationToken)
    {
        DataRightsPropertyProjection current = await this.GetOrCreateAsync(
            property.ScopeId,
            property.PropertyId,
            cancellationToken).ConfigureAwait(false);
        current.ApplyPolicy(
            property.ProcessingStatus,
            property.GovernancePolicy,
            property.SourceVersion);
    }

    public async Task<DataRightsPropertyPolicySnapshot?> GetPolicyAsync(
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        DataRightsPropertyProjection? property = await dbContext.PropertyProjections
            .AsNoTracking()
            .Include(item => item.GovernancePolicy)
            .ThenInclude(policy => policy!.Acknowledgements)
            .FirstOrDefaultAsync(item => item.Id == propertyId, cancellationToken)
            .ConfigureAwait(false);
        return property is null
            ? null
            : new DataRightsPropertyPolicySnapshot(
                property.IsKnown,
                property.Status == PropertyStatus.Active,
                property.ProcessingStatus,
                MapPolicy(property.GovernancePolicy),
                property.PolicySourceVersion);
    }

    private async Task<DataRightsPropertyProjection> GetOrCreateAsync(
        string scopeId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        DataRightsPropertyProjection? current = dbContext.PropertyProjections.Local
            .FirstOrDefault(item => item.ScopeId == scopeId && item.Id == propertyId) ??
            await dbContext.PropertyProjections.FirstOrDefaultAsync(
                item => item.Id == propertyId,
                cancellationToken).ConfigureAwait(false);
        if (current is not null)
        {
            return current;
        }

        current = new DataRightsPropertyProjection(
            scopeId,
            propertyId,
            null,
            PropertyStatus.Unknown,
            0);
        dbContext.PropertyProjections.Add(current);
        return current;
    }

    private static PropertyGovernancePolicyBinding? MapPolicy(
        DataRightsPropertyPolicyBinding? policy) =>
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
