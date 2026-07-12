namespace BunkFy.Modules.Ingestion.Persistence.Repositories;

using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using Microsoft.EntityFrameworkCore;

internal sealed class ChangeProposalRepository(IngestionDbContext dbContext) : IChangeProposalRepository
{
    public Task<ChangeProposal?> GetAsync(Guid proposalId, CancellationToken cancellationToken) =>
        dbContext.ChangeProposals.FirstOrDefaultAsync(proposal => proposal.Id == proposalId, cancellationToken);

    public Task<ChangeProposal?> FindByReceiptAsync(Guid receiptId, CancellationToken cancellationToken) =>
        dbContext.ChangeProposals.FirstOrDefaultAsync(proposal => proposal.ReceiptId == receiptId, cancellationToken);

    public Task AddAsync(ChangeProposal proposal, CancellationToken cancellationToken)
    {
        dbContext.ChangeProposals.Add(proposal);
        return Task.CompletedTask;
    }
}
