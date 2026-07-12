namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using Microsoft.EntityFrameworkCore;

internal sealed class AdapterIngressCredentialRepository(IngestionDbContext dbContext)
    : IAdapterIngressCredentialRepository, IAdapterIngressCredentialReader
{
    private static readonly TimeSpan AuthenticationTelemetryInterval = TimeSpan.FromMinutes(5);

    public async Task<int?> GetAvailableSlotAsync(
        Guid connectionId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        _ = await dbContext.AdapterIngressCredentials
            .Where(credential => credential.ConnectionId == connectionId &&
                                 credential.State == AdapterIngressCredentialState.Active &&
                                 credential.ExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(
                update => update.SetProperty(
                    credential => credential.State,
                    AdapterIngressCredentialState.Expired),
                cancellationToken)
            .ConfigureAwait(false);
        int[] occupied = await dbContext.AdapterIngressCredentials
            .AsNoTracking()
            .Where(credential => credential.ConnectionId == connectionId &&
                                 credential.State == AdapterIngressCredentialState.Active)
            .Select(credential => credential.Slot)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return Enumerable.Range(1, AdapterIngressCredential.MaximumActiveCredentialsPerConnection)
            .Cast<int?>()
            .FirstOrDefault(slot => !occupied.Contains(slot!.Value));
    }

    public Task<AdapterIngressCredential?> GetAsync(
        Guid connectionId,
        Guid credentialId,
        CancellationToken cancellationToken) => dbContext.AdapterIngressCredentials.FirstOrDefaultAsync(
        credential => credential.ConnectionId == connectionId && credential.Id == credentialId,
        cancellationToken);

    public Task<AdapterIngressCredential?> GetForAuthenticationAsync(
        Guid connectionId,
        Guid credentialId,
        CancellationToken cancellationToken) => dbContext.AdapterIngressCredentials
        .AsNoTracking()
        .FirstOrDefaultAsync(
            credential => credential.ConnectionId == connectionId && credential.Id == credentialId,
            cancellationToken);

    public Task AddAsync(AdapterIngressCredential credential, CancellationToken cancellationToken)
    {
        dbContext.AdapterIngressCredentials.Add(credential);
        return Task.CompletedTask;
    }

    public async Task MarkAuthenticatedAsync(
        Guid credentialId,
        DateTimeOffset authenticatedAtUtc,
        CancellationToken cancellationToken)
    {
        DateTimeOffset updateBeforeUtc = authenticatedAtUtc.Subtract(AuthenticationTelemetryInterval);
        _ = await dbContext.AdapterIngressCredentials
            .Where(credential => credential.Id == credentialId &&
                                 (credential.LastAuthenticatedAtUtc == null ||
                                  credential.LastAuthenticatedAtUtc < updateBeforeUtc))
            .ExecuteUpdateAsync(
                update => update.SetProperty(
                    credential => credential.LastAuthenticatedAtUtc,
                    authenticatedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AdapterIngressCredentialListResponse> ListAsync(
        Guid connectionId,
        PageRequest pageRequest,
        CancellationToken cancellationToken)
    {
        IQueryable<AdapterIngressCredential> query = dbContext.AdapterIngressCredentials
            .AsNoTracking()
            .Where(credential => credential.ConnectionId == connectionId);
        long totalCount = await query.LongCountAsync(cancellationToken).ConfigureAwait(false);
        AdapterIngressCredentialDto[] values = await query
            .OrderByDescending(credential => credential.CreatedAtUtc)
            .ThenBy(credential => credential.Id)
            .Skip(pageRequest.SkipCount)
            .Take(pageRequest.PageSize)
            .Select(credential => new AdapterIngressCredentialDto(
                credential.Id,
                credential.ConnectionId,
                credential.Slot,
                credential.Label,
                (AdapterIngressCredentialStatus)(int)credential.State,
                credential.ExpiresAtUtc,
                credential.CreatedBy,
                credential.CreatedAtUtc,
                credential.RevokedBy,
                credential.RevokedAtUtc,
                credential.LastAuthenticatedAtUtc,
                credential.Version))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new AdapterIngressCredentialListResponse(
            values, pageRequest.Page, pageRequest.PageSize, totalCount);
    }
}
