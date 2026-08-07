namespace BunkFy.Modules.Guests.Tests;

using BunkFy.Modules.Guests.Application;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Handlers;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using BunkFy.Modules.Guests.Domain.Models;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestDataHoldCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Place_and_equivalent_retry_return_the_same_immutable_receipt()
    {
        GuestProfile profile = CreateProfile();
        RecordingHoldRepository holds = new();
        PlaceGuestDataHoldCommandHandler handler = new(
            new StubGuestRepository(profile),
            holds,
            new NoopGuestOperationLock(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        PlaceGuestDataHoldCommand command = new(
            Guid.NewGuid(),
            profile.OriginPropertyId,
            profile.Id,
            profile.Version,
            GuestDataHoldReasonCodes.RegulatoryRequest,
            "user:privacy");

        Result<GuestDataHoldReceiptDto> first =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<GuestDataHoldReceiptDto> replay =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, replay.Value);
        Assert.Single(holds.Holds);
        Assert.Single(holds.Receipts);
    }

    [Fact]
    public async Task Changed_place_retry_and_stale_guest_are_rejected()
    {
        GuestProfile profile = CreateProfile();
        RecordingHoldRepository holds = new();
        PlaceGuestDataHoldCommandHandler handler = new(
            new StubGuestRepository(profile),
            holds,
            new NoopGuestOperationLock(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        PlaceGuestDataHoldCommand command = new(
            Guid.NewGuid(),
            profile.OriginPropertyId,
            profile.Id,
            profile.Version,
            GuestDataHoldReasonCodes.Dispute,
            "user:privacy");

        Assert.True((await handler.HandleAsync(command, CancellationToken.None)).IsSuccess);
        Result<GuestDataHoldReceiptDto> changed = await handler.HandleAsync(
            command with { ReasonCode = GuestDataHoldReasonCodes.LegalObligation },
            CancellationToken.None);
        Result<GuestDataHoldReceiptDto> stale = await handler.HandleAsync(
            command with
            {
                IdempotencyKey = Guid.NewGuid(),
                ExpectedGuestVersion = profile.Version + 1
            },
            CancellationToken.None);

        Assert.Equal(GuestsApplicationErrors.DataHoldIdempotencyConflict, changed.Error);
        Assert.Equal(GuestsApplicationErrors.DataHoldGuestVersionConflict, stale.Error);
        Assert.Single(holds.Holds);
    }

    [Fact]
    public async Task Place_rejects_anonymised_guest_even_at_the_current_version()
    {
        GuestProfile profile = CreateProfile();
        Assert.True(profile.Anonymise(
            profile.Version,
            "user:privacy",
            Guid.NewGuid(),
            Now).IsSuccess);
        RecordingHoldRepository holds = new();
        PlaceGuestDataHoldCommandHandler handler = new(
            new StubGuestRepository(profile),
            holds,
            new NoopGuestOperationLock(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        PlaceGuestDataHoldCommand command = new(
            Guid.NewGuid(),
            profile.OriginPropertyId,
            profile.Id,
            profile.Version,
            GuestDataHoldReasonCodes.RegulatoryRequest,
            "user:privacy");

        Result<GuestDataHoldReceiptDto> result =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(GuestsApplicationErrors.DataHoldGuestNotEligible, result.Error);
        Assert.Empty(holds.Holds);
        Assert.Empty(holds.Receipts);
    }

    [Fact]
    public async Task Release_requires_exact_hold_version_and_replays_exactly()
    {
        GuestProfile profile = CreateProfile();
        GuestDataHold hold = GuestDataHold.Place(
            Guid.NewGuid(),
            profile.ScopeId,
            profile.OriginPropertyId,
            profile.Id,
            GuestDataHoldReasonCodes.SecurityInvestigation,
            "user:privacy",
            Now.AddMinutes(-1)).Value;
        RecordingHoldRepository holds = new([hold]);
        ReleaseGuestDataHoldCommandHandler handler = new(
            new StubGuestRepository(profile),
            holds,
            new NoopGuestOperationLock(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        ReleaseGuestDataHoldCommand command = new(
            Guid.NewGuid(),
            profile.OriginPropertyId,
            profile.Id,
            hold.Id,
            profile.Version,
            hold.Version,
            "user:decision-maker");

        Result<GuestDataHoldReceiptDto> released =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<GuestDataHoldReceiptDto> replay =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(released.IsSuccess);
        Assert.Equal(released.Value, replay.Value);
        Assert.Equal(GuestDataHoldState.Released, hold.State);
        Assert.Single(holds.Receipts);
    }

    private static GuestProfile CreateProfile() => GuestProfile.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        "Guest",
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        "user:creator",
        Guid.NewGuid(),
        Now.AddHours(-1)).Value;

    private sealed class StubGuestRepository(GuestProfile profile)
        : IGuestProfileRepository
    {
        public Task AddUnderAcquiredOperationLockAsync(
            GuestProfile added,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<GuestProfile?> GetByIdAsync(
            Guid guestId,
            CancellationToken cancellationToken) => Task.FromResult(
            profile.Id == guestId ? profile : null);

        public Task<GuestProfile?> GetVisibleAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) =>
            this.GetForDataRightsAsync(propertyId, guestId, cancellationToken);

        public Task<GuestProfile?> GetForDataRightsAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) => Task.FromResult(
            profile.OriginPropertyId == propertyId && profile.Id == guestId
                ? profile
                : null);

        public Task<GuestListResponse> ListVisibleAsync(
            Guid propertyId,
            string? search,
            GuestStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingHoldRepository(
        IEnumerable<GuestDataHold>? initial = null)
        : IGuestDataHoldRepository
    {
        public List<GuestDataHold> Holds { get; } = initial?.ToList() ?? [];
        public List<GuestDataHoldReceipt> Receipts { get; } = [];

        public Task<GuestDataHoldReceipt?> FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Receipts.SingleOrDefault(receipt =>
                receipt.IdempotencyKey == idempotencyKey));

        public Task<GuestDataHold?> GetAsync(
            Guid propertyId,
            Guid guestId,
            Guid holdId,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Holds.SingleOrDefault(hold =>
                hold.PropertyId == propertyId &&
                hold.GuestId == guestId &&
                hold.Id == holdId));

        public Task<IReadOnlyCollection<GuestDataHold>> ListAsync(
            Guid propertyId,
            Guid guestId,
            GuestDataHoldStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(GuestDataHold hold, CancellationToken cancellationToken)
        {
            this.Holds.Add(hold);
            return Task.CompletedTask;
        }

        public Task AddReceiptAsync(
            GuestDataHoldReceipt receipt,
            CancellationToken cancellationToken)
        {
            this.Receipts.Add(receipt);
            return Task.CompletedTask;
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
