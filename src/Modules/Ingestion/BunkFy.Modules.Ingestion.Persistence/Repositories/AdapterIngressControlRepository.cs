namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using System.Data.Common;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Controls;
using Microsoft.EntityFrameworkCore;

internal sealed class AdapterIngressControlRepository(IngestionDbContext dbContext)
    : IAdapterIngressControlRepository
{
    public Task<AdapterIngressTenantControl?> GetTenantAsync(CancellationToken cancellationToken) =>
        dbContext.AdapterIngressTenantControls.SingleOrDefaultAsync(cancellationToken);

    public Task<AdapterIngressGlobalControl?> GetGlobalAsync(CancellationToken cancellationToken) =>
        dbContext.AdapterIngressGlobalControls.SingleOrDefaultAsync(
            control => control.Id == AdapterIngressGlobalControl.SingletonId,
            cancellationToken);

    public async Task<AdapterIngressControlSnapshot?> ReadAdmissionAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await dbContext.AdapterIngressTenantControls
                .AsNoTracking()
                .Select(control => new
                {
                    IsTenant = true,
                    IsStopped = control.IsSuspended
                })
                .Concat(dbContext.AdapterIngressGlobalControls
                    .AsNoTracking()
                    .Where(control => control.Id == AdapterIngressGlobalControl.SingletonId)
                    .Select(control => new
                    {
                        IsTenant = false,
                        control.IsStopped
                    }))
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            return new AdapterIngressControlSnapshot(
                rows.Any(row => row.IsTenant && row.IsStopped),
                rows.Any(row => !row.IsTenant && row.IsStopped));
        }
        catch (DbException)
        {
            return null;
        }
        catch (TimeoutException)
        {
            return null;
        }
    }

    public void Add(AdapterIngressTenantControl control) =>
        dbContext.AdapterIngressTenantControls.Add(control);

    public void Add(AdapterIngressGlobalControl control) =>
        dbContext.AdapterIngressGlobalControls.Add(control);
}
