namespace BunkFy.Modules.Guests.Persistence.Repositories;

using Gma.Framework.ProjectionRebuild;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Properties.Contracts;

internal sealed class GuestsPropertiesProjectionRebuildWriter(
    IGuestPropertyProjectionRepository repository,
    GuestsDbContext dbContext)
    : IProjectionRebuildWriter<PropertyTopologyProjectionExport>
{
    public async Task<ProjectionWriteResult> WriteAsync(
        ProjectionRebuildRequest request,
        IReadOnlyCollection<PropertyTopologyProjectionExport> snapshots,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshots);

        if (request.DryRun)
        {
            return new ProjectionWriteResult(writtenCount: 0, skippedCount: snapshots.Count);
        }

        foreach (PropertyTopologyProjectionExport property in snapshots)
        {
            await repository.ApplyTopologyAsync(
                new GuestPropertyTopologyWriteModel(
                    property.TenantId,
                    property.PropertyId,
                    property.Name,
                    property.Status,
                    property.Version),
                cancellationToken).ConfigureAwait(false);
            await repository.ApplyPolicyAsync(
                new GuestPropertyPolicyWriteModel(
                    property.TenantId,
                    property.PropertyId,
                    property.ProcessingStatus,
                    property.GovernancePolicy,
                    property.Version),
                cancellationToken).ConfigureAwait(false);
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ProjectionWriteResult(snapshots.Count);
    }
}
