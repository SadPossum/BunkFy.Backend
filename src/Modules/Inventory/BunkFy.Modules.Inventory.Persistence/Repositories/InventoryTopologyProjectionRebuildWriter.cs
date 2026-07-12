namespace BunkFy.Modules.Inventory.Persistence.Repositories;

using Gma.Framework.ProjectionRebuild;
using Gma.Framework.Runtime.Time;
using BunkFy.Modules.Inventory.Application.Ports;
using BunkFy.Modules.Properties.Contracts;

internal sealed class InventoryTopologyProjectionRebuildWriter(
    IInventoryTopologyRepository topologyRepository,
    IRoomInventoryConfigurationRepository configurationRepository,
    InventoryDbContext dbContext,
    ISystemClock clock)
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
            DateTimeOffset observedAtUtc = clock.UtcNow;
            await topologyRepository.ApplyPropertyAsync(
                new(
                    property.TenantId,
                    property.PropertyId,
                    property.Name,
                    property.Code,
                    property.TimeZoneId,
                    property.Status,
                    property.Version),
                cancellationToken).ConfigureAwait(false);

            foreach (RoomTopologyProjectionExport room in property.Rooms)
            {
                await topologyRepository.ApplyRoomAsync(
                    new(
                        property.TenantId,
                        property.PropertyId,
                        room.RoomId,
                        room.Name,
                        room.BuildingLabel,
                        room.FloorLabel,
                        room.Status,
                        room.Version),
                    cancellationToken).ConfigureAwait(false);
                await configurationRepository.EnsureAsync(
                    property.TenantId,
                    property.PropertyId,
                    room.RoomId,
                    observedAtUtc,
                    cancellationToken).ConfigureAwait(false);

                foreach (BedTopologyProjectionExport bed in room.Beds)
                {
                    await topologyRepository.ApplyBedAsync(
                        new(
                            property.TenantId,
                            property.PropertyId,
                            room.RoomId,
                            bed.BedId,
                            bed.Label,
                            bed.Status,
                            bed.Version),
                        cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ProjectionWriteResult(snapshots.Count);
    }
}
