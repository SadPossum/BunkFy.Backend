namespace BunkFy.Modules.Ingestion.Tests.Application;

using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Domain.LegalHolds;
using Xunit;

[Trait("Category", "Unit")]
public sealed class LegalHoldHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Placement_advances_fence_and_records_actor()
    {
        FakeLegalHoldRepository repository = new();
        FakeRetentionFence fence = new(true);
        PlaceLegalHoldCommandHandler handler = new(
            repository, fence, new TestScope(), new TestClock(), new FixedIds());

        var result = await handler.HandleAsync(
            new PlaceLegalHoldCommand(Guid.NewGuid(), "Regulatory request", "admin-api:user:42"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, fence.Advances);
        Assert.Equal("admin-api:user:42", result.Value.PlacedBy);
        Assert.Equal(LegalHoldState.Active, Assert.Single(repository.Items).State);
    }

    [Fact]
    public async Task Placement_fails_for_unknown_property_or_inflight_purge()
    {
        FakeLegalHoldRepository repository = new();
        PlaceLegalHoldCommand command = new(Guid.NewGuid(), "Regulatory request", "user:legal");

        var unknown = await new PlaceLegalHoldCommandHandler(
            repository, new FakeRetentionFence(false), new TestScope(), new TestClock(), new FixedIds())
            .HandleAsync(command, CancellationToken.None);
        repository.HasPurging = true;
        var purging = await new PlaceLegalHoldCommandHandler(
            repository, new FakeRetentionFence(true), new TestScope(), new TestClock(), new FixedIds())
            .HandleAsync(command, CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.PropertyNotFound, unknown.Error);
        Assert.Equal(IngestionApplicationErrors.LegalHoldPurgeInProgress, purging.Error);
        Assert.Empty(repository.Items);
    }

    [Fact]
    public async Task Release_requires_matching_property_and_advances_fence()
    {
        Guid propertyId = Guid.NewGuid();
        LegalHold legalHold = LegalHold.Place(
            Guid.NewGuid(), "tenant-a", propertyId, "Investigation", "user:legal", Now).Value;
        FakeLegalHoldRepository repository = new();
        repository.Items.Add(legalHold);
        FakeRetentionFence fence = new(true);
        ReleaseLegalHoldCommandHandler handler = new(repository, fence, new TestClock());

        var wrongProperty = await handler.HandleAsync(
            new ReleaseLegalHoldCommand(
                Guid.NewGuid(), legalHold.Id, 1, "Closed", "user:legal"),
            CancellationToken.None);
        var released = await handler.HandleAsync(
            new ReleaseLegalHoldCommand(
                propertyId, legalHold.Id, 1, "Closed", "user:legal"),
            CancellationToken.None);

        Assert.Equal(IngestionApplicationErrors.LegalHoldNotFound, wrongProperty.Error);
        Assert.True(released.IsSuccess);
        Assert.Equal(1, fence.Advances);
        Assert.Equal(BunkFy.Modules.Ingestion.Contracts.LegalHoldStatus.Released, released.Value.Status);
    }

    private sealed class FakeLegalHoldRepository : ILegalHoldRepository
    {
        public List<LegalHold> Items { get; } = [];
        public bool HasPurging { get; set; }

        public Task<bool> HasPurgingRawPayloadsAsync(
            Guid propertyId,
            CancellationToken cancellationToken) => Task.FromResult(this.HasPurging);

        public Task<LegalHold?> GetAsync(
            Guid propertyId,
            Guid holdId,
            CancellationToken cancellationToken) => Task.FromResult<LegalHold?>(this.Items.FirstOrDefault(
            legalHold => legalHold.PropertyId == propertyId && legalHold.Id == holdId));

        public Task AddAsync(LegalHold legalHold, CancellationToken cancellationToken)
        {
            this.Items.Add(legalHold);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRetentionFence(bool exists) : IRetentionFenceRepository
    {
        public int Advances { get; private set; }

        public Task<bool> TryAdvanceAsync(Guid propertyId, CancellationToken cancellationToken)
        {
            if (exists)
            {
                this.Advances++;
            }

            return Task.FromResult(exists);
        }
    }

    private sealed class TestScope : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class FixedIds : IIdGenerator
    {
        public Guid NewId() => Guid.Parse("c2000000-0000-0000-0000-000000000001");
    }
}
