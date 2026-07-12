namespace BunkFy.Modules.Ingestion.Tests.Application;

using Gma.Framework.Pagination;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Queries;
using BunkFy.Modules.Ingestion.Contracts;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ChangeProposalQueryHandlerTests
{
    [Fact]
    public async Task List_rejects_an_undefined_status_without_querying_persistence()
    {
        StubChangeProposalReader reader = new();
        ListChangeProposalsQueryHandler handler = new(reader);

        var result = await handler.HandleAsync(
            new ListChangeProposalsQuery(Guid.NewGuid(), (ChangeProposalStatus)999, 1, 25),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.ProposalStatusInvalid, result.Error);
        Assert.False(reader.WasCalled);
    }

    private sealed class StubChangeProposalReader : IChangeProposalReader
    {
        public bool WasCalled { get; private set; }

        public Task<ChangeProposalDto?> GetAsync(
            Guid propertyId,
            Guid proposalId,
            CancellationToken cancellationToken) => Task.FromResult<ChangeProposalDto?>(null);

        public Task<ChangeProposalListResponse> ListAsync(
            Guid propertyId,
            ChangeProposalStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken)
        {
            this.WasCalled = true;
            return Task.FromResult(new ChangeProposalListResponse([], pageRequest.Page, pageRequest.PageSize, 0));
        }
    }
}
