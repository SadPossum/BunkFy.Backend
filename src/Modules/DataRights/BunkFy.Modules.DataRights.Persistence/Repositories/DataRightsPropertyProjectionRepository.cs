namespace BunkFy.Modules.DataRights.Persistence.Repositories;

using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

internal sealed class DataRightsPropertyProjectionRepository(DataRightsDbContext dbContext)
    : IDataRightsPropertyProjectionRepository
{
    private const string PropertyProjectionLockPrefix =
        "bunkfy:data-rights:property-projection:";

    public async Task ApplyTopologyAsync(
        DataRightsPropertyTopologyWriteModel property,
        CancellationToken cancellationToken)
    {
        await this.AcquirePropertyProjectionLockAsync(
            property.ScopeId,
            property.PropertyId,
            cancellationToken).ConfigureAwait(false);
        DataRightsPropertyProjection current = await this.GetOrCreateAsync(
            property.ScopeId,
            property.PropertyId,
            cancellationToken).ConfigureAwait(false);
        current.ApplyTopology(
            property.Name,
            property.TimeZoneId,
            property.Status,
            property.SourceVersion);
    }

    public async Task ApplyPolicyAsync(
        DataRightsPropertyPolicyWriteModel property,
        CancellationToken cancellationToken)
    {
        await this.AcquirePropertyProjectionLockAsync(
            property.ScopeId,
            property.PropertyId,
            cancellationToken).ConfigureAwait(false);
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
                property.Status,
                property.TimeZoneId,
                property.ProcessingStatus,
                MapPolicy(property.GovernancePolicy),
                property.TopologySourceVersion,
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
            null,
            PropertyStatus.Unknown,
            0);
        dbContext.PropertyProjections.Add(current);
        return current;
    }

    private async Task AcquirePropertyProjectionLockAsync(
        string tenantId,
        Guid propertyId,
        CancellationToken cancellationToken)
    {
        string scopeId = tenantId?.Trim() ?? string.Empty;
        if (scopeId.Length == 0 ||
            !string.Equals(scopeId, dbContext.CurrentScopeId, StringComparison.Ordinal) ||
            propertyId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A Data Rights property projection lock requires valid scoped coordinates.");
        }

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "A Data Rights property projection lock requires an active database transaction.");
        }

        await EfTransactionKeyLock.AcquireAsync(
            dbContext,
            PropertyProjectionLockPrefix + scopeId + ':' + propertyId.ToString("N"),
            cancellationToken).ConfigureAwait(false);
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
