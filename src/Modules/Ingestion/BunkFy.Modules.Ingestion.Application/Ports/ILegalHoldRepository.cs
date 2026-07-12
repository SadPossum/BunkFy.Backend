namespace BunkFy.Modules.Ingestion.Application.Ports;

using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;

public interface ILegalHoldRepository
{
    Task<bool> HasPurgingRawPayloadsAsync(Guid propertyId, CancellationToken cancellationToken);
    Task<LegalHold?> GetAsync(Guid propertyId, Guid holdId, CancellationToken cancellationToken);
    Task AddAsync(LegalHold legalHold, CancellationToken cancellationToken);
}

public interface ILegalHoldReader
{
    Task<LegalHoldDto?> GetAsync(Guid propertyId, Guid holdId, CancellationToken cancellationToken);
    Task<LegalHoldListResponse> ListAsync(
        Guid propertyId,
        LegalHoldStatus? status,
        PageRequest pageRequest,
        CancellationToken cancellationToken);
}
