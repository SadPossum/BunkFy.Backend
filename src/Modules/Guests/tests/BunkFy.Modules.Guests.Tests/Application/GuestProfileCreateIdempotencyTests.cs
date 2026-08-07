namespace BunkFy.Modules.Guests.Tests.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.Guests.Application;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Handlers;
using BunkFy.Modules.Guests.Application.Policies;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestProfileCreateIdempotencyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 7, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task New_creation_locks_and_reads_before_adding_the_operation_owned_guest()
    {
        List<string> sequence = [];
        RecordingGuestRepository profiles = new(sequence: sequence);
        RecordingGuestOperationLock operationLock = new(() => sequence.Add("lock"));
        Guid operationId = Guid.NewGuid();
        CreateGuestProfileCommand command = CreateCommand(operationId, Guid.NewGuid());
        CreateGuestProfileCommandHandler handler = CreateHandler(
            profiles,
            operationLock,
            new TestIdGenerator());

        Result<GuestMutationReceiptDto> result = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(operationId, result.Value.GuestId);
        Assert.Equal(operationId, profiles.Added?.Id);
        Assert.Equal(["lock", "read", "add"], sequence);
        Assert.Equal(
            (TestScopeContext.TenantId, operationId),
            Assert.Single(operationLock.GuestAcquisitions));
        Assert.IsType<IGuestsPersistenceRetryableCommand>(command, exactMatch: false);
    }

    [Fact]
    public async Task Normalized_retry_returns_the_current_receipt_without_recreating_the_guest()
    {
        Guid operationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        GuestProfile existing = CreateProfile(operationId, propertyId);
        Assert.True(existing.Archive(
            existing.Version,
            "user:archiver",
            Guid.NewGuid(),
            Now.AddMinutes(1)).IsSuccess);
        RecordingGuestRepository profiles = new(existing);
        RecordingGuestOperationLock operationLock = new();
        CreateGuestProfileCommandHandler handler = CreateHandler(
            profiles,
            operationLock,
            new ThrowingIdGenerator());

        Result<GuestMutationReceiptDto> result = await handler.HandleAsync(
            new CreateGuestProfileCommand(
                operationId,
                propertyId,
                "  Maya Chen  ",
                " Maya Q. Chen ",
                " MAYA@EXAMPLE.TEST ",
                " +44 20 1234 5678 ",
                new DateOnly(1990, 2, 3),
                " gb ",
                " en-GB ",
                " Prefers a lower bunk. ",
                "user:retrying-operator"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id, result.Value.GuestId);
        Assert.Equal(existing.Version, result.Value.Version);
        Assert.Equal(GuestStatus.Archived, result.Value.Status);
        Assert.Null(profiles.Added);
        Assert.Equal(1, profiles.ByIdReads);
    }

    [Fact]
    public async Task Country_policy_is_rechecked_before_an_existing_operation_can_replay()
    {
        Guid operationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        RecordingGuestRepository profiles = new(CreateProfile(operationId, propertyId));
        RecordingGuestOperationLock operationLock = new();
        CreateGuestProfileCommandHandler handler = CreateHandler(
            profiles,
            operationLock,
            new ThrowingIdGenerator(),
            new DeniedCountryPolicyAdmission());

        Result<GuestMutationReceiptDto> result = await handler.HandleAsync(
            CreateCommand(operationId, propertyId),
            CancellationToken.None);

        Assert.Equal(
            GuestsApplicationErrors.CountryPolicyDenied(
                CountryPolicyDecisionReason.MissingBinding),
            result.Error);
        Assert.Empty(operationLock.GuestAcquisitions);
        Assert.Equal(0, profiles.ByIdReads);
        Assert.Null(profiles.Added);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reusing_an_operation_for_changed_profile_or_property_conflicts(
        bool changeProperty)
    {
        Guid operationId = Guid.NewGuid();
        Guid propertyId = Guid.NewGuid();
        RecordingGuestRepository profiles = new(CreateProfile(operationId, propertyId));
        RecordingGuestOperationLock operationLock = new();
        CreateGuestProfileCommand original = CreateCommand(operationId, propertyId);
        CreateGuestProfileCommand changed = changeProperty
            ? original with { PropertyId = Guid.NewGuid() }
            : original with { DisplayName = "Different Guest" };

        Result<GuestMutationReceiptDto> result = await CreateHandler(
            profiles,
            operationLock,
            new ThrowingIdGenerator()).HandleAsync(
                changed,
                CancellationToken.None);

        Assert.Equal(GuestsApplicationErrors.CreationOperationConflict, result.Error);
        Assert.Null(profiles.Added);
        Assert.Equal(1, profiles.ByIdReads);
    }

    private static CreateGuestProfileCommandHandler CreateHandler(
        RecordingGuestRepository profiles,
        IGuestOperationLock operationLock,
        IIdGenerator idGenerator,
        IGuestCountryPolicyAdmission? countryPolicy = null)
    {
        TestScopeContext scopeContext = new();
        return new(
            profiles,
            new GuestMutationCoordinator(profiles, operationLock, scopeContext),
            countryPolicy ?? new AllowedCountryPolicyAdmission(),
            scopeContext,
            new TestClock(),
            idGenerator);
    }

    private static CreateGuestProfileCommand CreateCommand(
        Guid operationId,
        Guid propertyId) => new(
        operationId,
        propertyId,
        "Maya Chen",
        "Maya Q. Chen",
        "maya@example.test",
        "+44 20 1234 5678",
        new DateOnly(1990, 2, 3),
        "GB",
        "en-GB",
        "Prefers a lower bunk.",
        "user:creator");

    private static GuestProfile CreateProfile(Guid guestId, Guid propertyId) =>
        GuestProfile.Create(
            guestId,
            TestScopeContext.TenantId,
            propertyId,
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
            Now).Value;

    private sealed class RecordingGuestRepository(
        GuestProfile? existing = null,
        List<string>? sequence = null) : IGuestProfileRepository
    {
        public GuestProfile? Added { get; private set; }
        public int ByIdReads { get; private set; }

        public Task AddUnderAcquiredOperationLockAsync(
            GuestProfile profile,
            CancellationToken cancellationToken)
        {
            sequence?.Add("add");
            this.Added = profile;
            return Task.CompletedTask;
        }

        public Task<GuestProfile?> GetByIdAsync(
            Guid guestId,
            CancellationToken cancellationToken)
        {
            sequence?.Add("read");
            this.ByIdReads++;
            return Task.FromResult(existing?.Id == guestId ? existing : null);
        }

        public Task<GuestProfile?> GetVisibleAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GuestProfile?> GetForDataRightsAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GuestListResponse> ListVisibleAsync(
            Guid propertyId,
            string? search,
            GuestStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AllowedCountryPolicyAdmission : IGuestCountryPolicyAdmission
    {
        public Task<CountryPolicyDecision> EvaluateAsync(
            Guid propertyId,
            string purposeCode,
            CountryPolicySurface surface,
            string sourceProvenance,
            CancellationToken cancellationToken) => Task.FromResult(
            CountryPolicyDecision.Allow(new CountryPolicyEvidence(
                "GB",
                "gb-hostel",
                1,
                "eu-west-2",
                "uk-no-transfer",
                "guest-operational",
                1,
                new string('a', 64),
                purposeCode,
                surface,
                sourceProvenance,
                CountryPolicyApprovalState.Approved,
                Now.AddDays(-1),
                Now.AddDays(30),
                Now,
                [])));
    }

    private sealed class DeniedCountryPolicyAdmission : IGuestCountryPolicyAdmission
    {
        public Task<CountryPolicyDecision> EvaluateAsync(
            Guid propertyId,
            string purposeCode,
            CountryPolicySurface surface,
            string sourceProvenance,
            CancellationToken cancellationToken) => Task.FromResult(
            CountryPolicyDecision.Deny(CountryPolicyDecisionReason.MissingBinding));
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public const string TenantId = "tenant-a";
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }

    private sealed class ThrowingIdGenerator : IIdGenerator
    {
        public Guid NewId() => throw new InvalidOperationException(
            "A replay must not allocate a new event id.");
    }
}
