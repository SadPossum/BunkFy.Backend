namespace BunkFy.Modules.Guests.Tests.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.Guests.Application;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Handlers;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.Aggregates;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestProfileMutationSerializationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Update_rechecks_visibility_after_acquiring_the_guest_lock()
    {
        GuestProfile profile = CreateProfile();
        RevocableGuestRepository profiles = new(profile);
        RecordingGuestOperationLock operationLock = new(profiles.RevokeVisibility);
        UpdateGuestProfileCommand command = new(
            profile.OriginPropertyId,
            profile.Id,
            "Changed",
            profile.LegalName,
            profile.Email,
            profile.Phone,
            profile.DateOfBirth,
            profile.NationalityCountryCode,
            profile.PreferredLanguageTag,
            profile.Notes,
            profile.Version,
            "user:operator");
        UpdateGuestProfileCommandHandler handler = new(
            profiles,
            operationLock,
            new AllowedCountryPolicyAdmission(),
            new TestClock(),
            new TestIdGenerator());

        Result<GuestMutationReceiptDto> result = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.Equal(GuestsApplicationErrors.GuestNotFound, result.Error);
        Assert.Equal("Original", profile.DisplayName);
        Assert.Equal(1, profile.Version);
        Assert.Equal(2, profiles.VisibleReads);
        Assert.Equal(
            (profile.ScopeId, profile.Id),
            Assert.Single(operationLock.GuestAcquisitions));
        Assert.IsType<IGuestsPersistenceRetryableCommand>(command, exactMatch: false);
    }

    [Fact]
    public async Task Archive_rechecks_visibility_after_acquiring_the_guest_lock()
    {
        GuestProfile profile = CreateProfile();
        RevocableGuestRepository profiles = new(profile);
        RecordingGuestOperationLock operationLock = new(profiles.RevokeVisibility);
        ArchiveGuestProfileCommand command = new(
            profile.OriginPropertyId,
            profile.Id,
            profile.Version,
            "user:operator");
        ArchiveGuestProfileCommandHandler handler = new(
            profiles,
            operationLock,
            new TestClock(),
            new TestIdGenerator());

        Result<GuestMutationReceiptDto> result = await handler.HandleAsync(
            command,
            CancellationToken.None);

        Assert.Equal(GuestsApplicationErrors.GuestNotFound, result.Error);
        Assert.Equal(GuestProfileState.Active, profile.Status);
        Assert.Equal(1, profile.Version);
        Assert.Equal(2, profiles.VisibleReads);
        Assert.Equal(
            (profile.ScopeId, profile.Id),
            Assert.Single(operationLock.GuestAcquisitions));
        Assert.IsType<IGuestsPersistenceRetryableCommand>(command, exactMatch: false);
    }

    private static GuestProfile CreateProfile() => GuestProfile.Create(
        Guid.NewGuid(),
        "tenant-a",
        Guid.NewGuid(),
        "Original",
        null,
        "guest@example.test",
        null,
        null,
        null,
        null,
        null,
        "user:creator",
        Guid.NewGuid(),
        Now.AddDays(-1)).Value;

    private sealed class RevocableGuestRepository(GuestProfile profile)
        : IGuestProfileRepository
    {
        private bool visible = true;

        public int VisibleReads { get; private set; }

        public void RevokeVisibility() => this.visible = false;

        public Task AddAsync(
            GuestProfile added,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<GuestProfile?> GetVisibleAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken)
        {
            this.VisibleReads++;
            return Task.FromResult(
                this.visible &&
                profile.OriginPropertyId == propertyId &&
                profile.Id == guestId
                    ? profile
                    : null);
        }

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

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
