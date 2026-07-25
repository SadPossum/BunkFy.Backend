namespace BunkFy.Modules.Guests.Tests.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Handlers;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using BunkFy.Modules.Guests.Domain.DataRights;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RestoreGuestAnonymisationCommandHandlerTests
{
    private static readonly DateTimeOffset OriginallyCompletedAtUtc =
        new(2026, 7, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReplayedAtUtc =
        OriginallyCompletedAtUtc.AddHours(2);

    [Fact]
    public async Task Restore_re_scrubs_profile_and_is_idempotent()
    {
        GuestProfile profile = CreateProfile();
        profile.ClearDomainEvents();
        RecordingRepository repository = new(profile);
        RecordingBoundary boundary = new();
        RestoreGuestAnonymisationCommandHandler handler = CreateHandler(
            repository,
            boundary,
            Guid.NewGuid());
        RestoreGuestAnonymisationCommand command =
            new(CreateRequest(profile.Id, profile.OriginPropertyId));

        Result<GuestAnonymisationRestoreReceipt> first =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<GuestAnonymisationRestoreReceipt> replay =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value.CanonicalSha256, replay.Value.CanonicalSha256);
        Assert.True(profile.MatchesAnonymisedState(
            OriginallyCompletedAtUtc));
        Assert.Null(profile.Email);
        Assert.Null(profile.Phone);
        Assert.NotNull(repository.Tombstone);
        Assert.True(repository.Tombstone.MatchesRestore(
            profile.Id,
            command.Request.LedgerEntryId,
            OriginallyCompletedAtUtc,
            command.Request.OwnerReceiptSha256));
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(2, boundary.CallCount);
    }

    [Fact]
    public async Task Restore_recreates_a_terminal_profile_when_backup_predates_it()
    {
        Guid guestId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        RecordingRepository repository = new(profile: null);
        RestoreGuestAnonymisationCommandHandler handler = CreateHandler(
            repository,
            new RecordingBoundary(),
            Guid.NewGuid());

        Result<GuestAnonymisationRestoreReceipt> result =
            await handler.HandleAsync(
                new RestoreGuestAnonymisationCommand(
                    CreateRequest(guestId, propertyId)),
                CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.Profile);
        Assert.Equal(guestId, repository.Profile.Id);
        Assert.Equal(propertyId, repository.Profile.OriginPropertyId);
        Assert.True(repository.Profile.MatchesAnonymisedState(
            OriginallyCompletedAtUtc));
        Assert.Equal(
            repository.Profile.Version,
            result.Value.ResultingGuestVersion);
        Assert.NotNull(repository.Tombstone);
        Assert.NotNull(repository.Receipt);
    }

    [Fact]
    public async Task Restore_rejects_a_conflicting_existing_receipt()
    {
        GuestProfile profile = CreateProfile();
        DataRightsAnonymisationRestoreRequest request =
            CreateRequest(profile.Id, profile.OriginPropertyId);
        GuestAnonymisationRestoreReceipt conflicting =
            GuestAnonymisationRestoreReceipt.Create(
                "tenant-a",
                request.LedgerEntryId,
                profile.Id,
                request.OwnerReceiptContractVersion,
                Guid.NewGuid(),
                request.OwnerReceiptSha256,
                resultingGuestVersion: 2,
                tombstoneRevision: 1,
                ReplayedAtUtc).Value;
        RecordingRepository repository = new(profile)
        {
            Receipt = conflicting
        };
        RestoreGuestAnonymisationCommandHandler handler = CreateHandler(
            repository,
            new RecordingBoundary(),
            Guid.NewGuid());

        Result<GuestAnonymisationRestoreReceipt> result =
            await handler.HandleAsync(
                new RestoreGuestAnonymisationCommand(request),
                CancellationToken.None);

        Assert.Equal(
            "Guests.AnonymisationRestoreProofConflict",
            result.Error.Code);
        Assert.Equal(GuestProfileState.Active, profile.Status);
        Assert.Equal(0, repository.AddCount);
    }

    private static RestoreGuestAnonymisationCommandHandler CreateHandler(
        RecordingRepository repository,
        RecordingBoundary boundary,
        params Guid[] eventIds) =>
        new(
            repository,
            boundary,
            new TestScopeContext(),
            new TestClock(),
            new QueueIdGenerator(eventIds));

    private static DataRightsAnonymisationRestoreRequest CreateRequest(
        Guid guestId,
        Guid propertyId) =>
        new(
            DataRightsAnonymisationRestoreContract.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            TenantSequence: 1,
            LedgerEntrySha256: new string('a', 64),
            propertyId,
            GuestsDataRightsCoordinates.Owner,
            GuestsDataRightsCoordinates.GuestProfileRecordType,
            guestId,
            OwnerReceiptContractVersion: 1,
            OwnerReceiptId: Guid.NewGuid(),
            OwnerReceiptSha256: new string('b', 64),
            OriginallyCompletedAtUtc);

    private static GuestProfile CreateProfile() => GuestProfile.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        "Maya Chen",
        "Maya Q. Chen",
        "maya@example.test",
        "+44 20 1234 5678",
        new DateOnly(1990, 2, 3),
        "GB",
        "en-GB",
        "Prefers a lower bunk.",
        "user:creator",
        Guid.NewGuid(),
        OriginallyCompletedAtUtc.AddDays(-1)).Value;

    private sealed class RecordingRepository(GuestProfile? profile)
        : IGuestAnonymisationRestoreRepository
    {
        public GuestProfile? Profile { get; private set; } = profile;
        public GuestAnonymisationTombstone? Tombstone { get; private set; }
        public GuestAnonymisationRestoreReceipt? Receipt { get; set; }
        public int AddCount { get; private set; }

        public Task<GuestProfile?> GetProfileAsync(
            Guid guestId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Profile?.Id == guestId ? this.Profile : null);

        public Task<GuestAnonymisationTombstone?> GetTombstoneAsync(
            Guid guestId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Tombstone?.Id == guestId ? this.Tombstone : null);

        public Task<GuestAnonymisationRestoreReceipt?> GetReceiptAsync(
            Guid ledgerEntryId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Receipt?.LedgerEntryId == ledgerEntryId
                    ? this.Receipt
                    : null);

        public Task AddAsync(
            GuestAnonymisationRestoreReceipt receipt,
            GuestAnonymisationTombstone? newTombstone,
            GuestProfile? newProfile,
            CancellationToken cancellationToken)
        {
            this.Receipt = receipt;
            this.Tombstone ??= newTombstone;
            this.Profile ??= newProfile;
            this.AddCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBoundary
        : IGuestAnonymisationExecutionBoundary
    {
        public int CallCount { get; private set; }

        public Task AcquireAsync(
            string tenantId,
            Guid guestId,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
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
        public DateTimeOffset UtcNow => ReplayedAtUtc;
    }

    private sealed class QueueIdGenerator(params Guid[] ids) : IIdGenerator
    {
        private readonly Queue<Guid> values = new(ids);

        public Guid NewId() => this.values.Dequeue();
    }
}
