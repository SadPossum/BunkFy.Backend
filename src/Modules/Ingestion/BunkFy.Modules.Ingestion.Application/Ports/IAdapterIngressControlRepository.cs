namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Modules.Ingestion.Domain.Controls;

public interface IAdapterIngressControlRepository
{
    Task<AdapterIngressTenantControl?> GetTenantAsync(CancellationToken cancellationToken);

    Task<AdapterIngressGlobalControl?> GetGlobalAsync(CancellationToken cancellationToken);

    Task<AdapterIngressControlSnapshot?> ReadAdmissionAsync(CancellationToken cancellationToken);

    void Add(AdapterIngressTenantControl control);

    void Add(AdapterIngressGlobalControl control);
}

public sealed record AdapterIngressControlSnapshot(
    bool IsTenantSuspended,
    bool IsGlobalStopped);
