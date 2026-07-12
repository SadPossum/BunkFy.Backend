namespace BunkFy.Modules.Ingestion.Application.Ports;

using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Contracts;

public interface IAdapterIngressCredentialReader
{
    Task<AdapterIngressCredentialListResponse> ListAsync(
        Guid connectionId,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
