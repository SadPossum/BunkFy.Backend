namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;

internal sealed partial class LocalFileTenantTerminationReplayStore
{
    public async Task<TenantTerminationReplayIntent?> ReadIntentAsync(
        string tenantId,
        Guid processId,
        CancellationToken cancellationToken)
    {
        string normalizedTenant = NormalizeTenant(tenantId);
        RequireProcessId(processId);
        await using ProcessLease lease = await this.AcquireAsync(
            processId,
            cancellationToken).ConfigureAwait(false);
        TrustedJournalState state = await this.LoadTrustedStateAsync(
            lease.DirectoryPath,
            normalizedTenant,
            processId,
            cancellationToken).ConfigureAwait(false);
        string intentId = TenantTerminationReplayProof
            .ComputeIntentLogicalEntryId(normalizedTenant, processId);
        return state.Records
            .Select(record => record.Entry)
            .SingleOrDefault(entry => string.Equals(
                entry.LogicalEntryId,
                intentId,
                StringComparison.Ordinal))
            ?.Intent;
    }
}
