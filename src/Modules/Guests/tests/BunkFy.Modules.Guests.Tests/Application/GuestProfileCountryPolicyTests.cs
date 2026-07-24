namespace BunkFy.Modules.Guests.Tests;

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
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestProfileCountryPolicyTests
{
    [Fact]
    public async Task Policy_denial_prevents_durable_guest_creation()
    {
        RecordingGuestRepository profiles = new();
        CreateGuestProfileCommandHandler handler = new(
            profiles,
            new DeniedCountryPolicyAdmission(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

        Result<GuestProfileDto> result = await handler.HandleAsync(
            new CreateGuestProfileCommand(
                Guid.NewGuid(),
                "Ada Guest",
                null,
                "ada@example.test",
                null,
                null,
                null,
                null,
                null,
                "user:staff"),
            CancellationToken.None);

        Assert.Equal(GuestsApplicationErrors.CountryPolicyDenied(CountryPolicyDecisionReason.MissingBinding), result.Error);
        Assert.Null(profiles.Added);
    }

    private sealed class DeniedCountryPolicyAdmission : IGuestCountryPolicyAdmission
    {
        public Task<CountryPolicyDecision> EvaluateAsync(
            Guid propertyId,
            string purposeCode,
            CountryPolicySurface surface,
            string sourceProvenance,
            CancellationToken cancellationToken) =>
            Task.FromResult(CountryPolicyDecision.Deny(CountryPolicyDecisionReason.MissingBinding));
    }

    private sealed class RecordingGuestRepository : IGuestProfileRepository
    {
        public GuestProfile? Added { get; private set; }

        public Task AddAsync(GuestProfile profile, CancellationToken cancellationToken)
        {
            this.Added = profile;
            return Task.CompletedTask;
        }

        public Task<GuestProfile?> GetVisibleAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) => Task.FromResult<GuestProfile?>(null);

        public Task<GuestProfile?> GetForDataRightsAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) => Task.FromResult<GuestProfile?>(null);

        public Task<GuestListResponse> ListVisibleAsync(
            Guid propertyId,
            string? search,
            GuestStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => Guid.CreateVersion7();
    }
}
