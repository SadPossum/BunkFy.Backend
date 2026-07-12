namespace BunkFy.Modules.Ingestion.Application.Ports;

using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Contracts;

public interface IChangeProposalReader
{
    Task<ChangeProposalDto?> GetAsync(Guid propertyId, Guid proposalId, CancellationToken cancellationToken);
    Task<ChangeProposalListResponse> ListAsync(
        Guid propertyId,
        ChangeProposalStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
