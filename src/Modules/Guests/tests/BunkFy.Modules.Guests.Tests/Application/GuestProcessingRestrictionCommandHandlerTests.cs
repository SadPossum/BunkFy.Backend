namespace BunkFy.Modules.Guests.Tests;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
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
public sealed class GuestProcessingRestrictionCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 24, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Apply_binds_exact_directive_and_equivalent_retry_returns_receipt()
    {
        GuestProfile profile = CreateProfile();
        GuestProcessingRestrictionProjection projection = CreateProjection(profile);
        RecordingRestrictionRepository restrictions = new();
        RecordingApprovalGate approvalGate = new();
        ApplyGuestProcessingRestrictionCommandHandler handler = new(
            new RecordingGuestRepository(profile),
            new RecordingProjectionRepository(projection),
            restrictions,
            approvalGate,
            new AllowedCountryPolicyAdmission(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        ApplyGuestProcessingRestrictionCommand command = new(
            Guid.NewGuid(),
            profile.OriginPropertyId,
            Guid.NewGuid(),
            4,
            profile.Id,
            profile.Version,
            ExpectedProjectionRevision: 0,
            "user:privacy");

        Result<GuestProcessingRestrictionReceiptDto> applied =
            await handler.HandleAsync(command, CancellationToken.None);
        Result<GuestProcessingRestrictionReceiptDto> replayed =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(applied.IsSuccess);
        Assert.Equal(applied.Value, replayed.Value);
        Assert.Equal(DataRightsOperation.Restriction, approvalGate.Request?.Operation);
        Assert.Equal(DataRightsRestrictionDirective.Apply, approvalGate.Request?.RestrictionDirective);
        Assert.Equal(profile.Version, approvalGate.Request?.RecordVersion);
        Assert.True(projection.IsRestricted);
        Assert.Equal(1, projection.ActiveRestrictionCount);
        Assert.Equal(1, projection.Revision);
        Assert.Single(restrictions.Rows);
        Assert.Single(restrictions.Receipts);
    }

    [Fact]
    public async Task Changed_apply_retry_is_rejected_without_another_transition()
    {
        GuestProfile profile = CreateProfile();
        GuestProcessingRestrictionProjection projection = CreateProjection(profile);
        RecordingRestrictionRepository restrictions = new();
        ApplyGuestProcessingRestrictionCommandHandler handler = new(
            new RecordingGuestRepository(profile),
            new RecordingProjectionRepository(projection),
            restrictions,
            new RecordingApprovalGate(),
            new AllowedCountryPolicyAdmission(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        ApplyGuestProcessingRestrictionCommand command = new(
            Guid.NewGuid(),
            profile.OriginPropertyId,
            Guid.NewGuid(),
            4,
            profile.Id,
            profile.Version,
            ExpectedProjectionRevision: 0,
            "user:privacy");

        Assert.True((await handler.HandleAsync(command, CancellationToken.None)).IsSuccess);
        Result<GuestProcessingRestrictionReceiptDto> conflict =
            await handler.HandleAsync(
                command with { ExpectedProjectionRevision = 1 },
                CancellationToken.None);

        Assert.Equal(GuestsApplicationErrors.RestrictionIdempotencyConflict, conflict.Error);
        Assert.Equal(1, projection.Revision);
        Assert.Single(restrictions.Receipts);
    }

    [Fact]
    public async Task Release_uses_release_directive_and_keeps_other_case_effective()
    {
        GuestProfile profile = CreateProfile();
        GuestProcessingRestrictionProjection projection = CreateProjection(profile);
        GuestProcessingRestriction first = CreateRestriction(
            profile,
            Guid.NewGuid(),
            approvalRevision: 2,
            Now.AddMinutes(-20));
        GuestProcessingRestriction second = CreateRestriction(
            profile,
            Guid.NewGuid(),
            approvalRevision: 3,
            Now.AddMinutes(-15));
        Assert.True(projection.Apply(0, 1, Now.AddMinutes(-20)).IsSuccess);
        Assert.True(projection.Apply(1, 1, Now.AddMinutes(-15)).IsSuccess);
        RecordingRestrictionRepository restrictions = new([first, second]);
        RecordingApprovalGate approvalGate = new();
        ReleaseGuestProcessingRestrictionCommandHandler handler = new(
            new RecordingGuestRepository(profile),
            new RecordingProjectionRepository(projection),
            restrictions,
            approvalGate,
            new AllowedCountryPolicyAdmission(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        ReleaseGuestProcessingRestrictionCommand command = new(
            Guid.NewGuid(),
            profile.OriginPropertyId,
            first.Id,
            Guid.NewGuid(),
            8,
            profile.Id,
            profile.Version,
            first.Version,
            projection.Revision,
            "user:decision-maker");

        Result<GuestProcessingRestrictionReceiptDto> released =
            await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(released.IsSuccess);
        Assert.Equal(DataRightsRestrictionDirective.Release, approvalGate.Request?.RestrictionDirective);
        Assert.Equal(GuestProcessingRestrictionState.Released, first.Status);
        Assert.Equal(GuestProcessingRestrictionState.Active, second.Status);
        Assert.True(projection.IsRestricted);
        Assert.Equal(1, projection.ActiveRestrictionCount);
        Assert.Equal(3, projection.Revision);
        Assert.True(released.Value.EffectiveRestricted);
        Assert.Equal(GuestProcessingRestrictionActionDto.Release, released.Value.Action);
    }

    [Fact]
    public async Task Denied_approval_does_not_change_projection_or_owner_state()
    {
        GuestProfile profile = CreateProfile();
        GuestProcessingRestrictionProjection projection = CreateProjection(profile);
        RecordingRestrictionRepository restrictions = new();
        ApplyGuestProcessingRestrictionCommandHandler handler = new(
            new RecordingGuestRepository(profile),
            new RecordingProjectionRepository(projection),
            restrictions,
            new RecordingApprovalGate(isApproved: false),
            new AllowedCountryPolicyAdmission(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

        Result<GuestProcessingRestrictionReceiptDto> result = await handler.HandleAsync(
            new(
                Guid.NewGuid(),
                profile.OriginPropertyId,
                Guid.NewGuid(),
                1,
                profile.Id,
                profile.Version,
                0,
                "user:privacy"),
            CancellationToken.None);

        Assert.Equal(GuestsApplicationErrors.DataRightsApprovalRequired, result.Error);
        Assert.False(projection.IsRestricted);
        Assert.Empty(restrictions.Rows);
        Assert.Empty(restrictions.Receipts);
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

    private static GuestProcessingRestrictionProjection CreateProjection(GuestProfile profile) =>
        GuestProcessingRestrictionProjection.Create(
            profile.ScopeId,
            profile.OriginPropertyId,
            profile.Id,
            GuestProcessingRestrictionContract.CurrentVersion,
            profile.CreatedAtUtc).Value;

    private static GuestProcessingRestriction CreateRestriction(
        GuestProfile profile,
        Guid caseId,
        long approvalRevision,
        DateTimeOffset appliedAtUtc) => GuestProcessingRestriction.Create(
        Guid.NewGuid(),
        profile.ScopeId,
        profile.OriginPropertyId,
        profile.Id,
        caseId,
        approvalRevision,
        profile.Version,
        "user:privacy",
        appliedAtUtc).Value;

    private sealed class RecordingGuestRepository(GuestProfile profile)
        : IGuestProfileRepository
    {
        public Task AddAsync(GuestProfile added, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

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

    private sealed class RecordingProjectionRepository(
        GuestProcessingRestrictionProjection projection)
        : IGuestProcessingRestrictionProjectionRepository
    {
        public Task<GuestProcessingRestrictionProjection?> GetAsync(
            Guid propertyId,
            Guid guestId,
            CancellationToken cancellationToken) => Task.FromResult(
            projection.PropertyId == propertyId && projection.GuestId == guestId
                ? projection
                : null);

        public Task EnsureAsync(
            string tenantId,
            Guid propertyId,
            Guid guestId,
            DateTimeOffset initializedAtUtc,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RecordingRestrictionRepository(
        IEnumerable<GuestProcessingRestriction>? rows = null)
        : IGuestProcessingRestrictionRepository
    {
        public List<GuestProcessingRestriction> Rows { get; } = rows?.ToList() ?? [];
        public List<GuestProcessingRestrictionReceipt> Receipts { get; } = [];

        public Task<GuestProcessingRestrictionReceipt?> FindReceiptByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Receipts.SingleOrDefault(receipt =>
                receipt.IdempotencyKey == idempotencyKey));

        public Task<GuestProcessingRestriction?> FindByApplyApprovalAsync(
            Guid propertyId,
            Guid guestId,
            Guid caseId,
            long approvalRevision,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Rows.SingleOrDefault(restriction =>
                restriction.PropertyId == propertyId &&
                restriction.GuestId == guestId &&
                restriction.ApplyCaseId == caseId &&
                restriction.ApplyApprovalRevision == approvalRevision));

        public Task<GuestProcessingRestriction?> FindByReleaseApprovalAsync(
            Guid propertyId,
            Guid guestId,
            Guid caseId,
            long approvalRevision,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Rows.SingleOrDefault(restriction =>
                restriction.PropertyId == propertyId &&
                restriction.GuestId == guestId &&
                restriction.ReleaseCaseId == caseId &&
                restriction.ReleaseApprovalRevision == approvalRevision));

        public Task<GuestProcessingRestriction?> GetAsync(
            Guid propertyId,
            Guid restrictionId,
            CancellationToken cancellationToken) => Task.FromResult(
            this.Rows.SingleOrDefault(restriction =>
                restriction.PropertyId == propertyId &&
                restriction.Id == restrictionId));

        public Task<IReadOnlyCollection<GuestProcessingRestriction>> ListActiveAsync(
            Guid propertyId,
            Guid guestId,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => Task.FromResult<
            IReadOnlyCollection<GuestProcessingRestriction>>(this.Rows
                .Where(restriction =>
                    restriction.PropertyId == propertyId &&
                    restriction.GuestId == guestId &&
                    restriction.Status == GuestProcessingRestrictionState.Active)
                .Skip(pageRequest.SkipCount)
                .Take(pageRequest.PageSize)
                .ToArray());

        public Task AddAsync(
            GuestProcessingRestriction restriction,
            CancellationToken cancellationToken)
        {
            this.Rows.Add(restriction);
            return Task.CompletedTask;
        }

        public Task AddReceiptAsync(
            GuestProcessingRestrictionReceipt receipt,
            CancellationToken cancellationToken)
        {
            this.Receipts.Add(receipt);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingApprovalGate(bool isApproved = true)
        : IDataRightsOperationApprovalGate
    {
        public DataRightsOperationApprovalRequest? Request { get; private set; }

        public Task<DataRightsOperationApprovalResult> EvaluateAsync(
            DataRightsOperationApprovalRequest request,
            CancellationToken cancellationToken)
        {
            this.Request = request;
            return Task.FromResult(isApproved
                ? DataRightsOperationApprovalResult.Approved
                : DataRightsOperationApprovalResult.Denied(
                    DataRightsOperationApprovalDenial.CaseNotApproved));
        }
    }

    private sealed class AllowedCountryPolicyAdmission : IGuestCountryPolicyAdmission
    {
        public Task<CountryPolicyDecision> EvaluateAsync(
            Guid propertyId,
            string purposeCode,
            CountryPolicySurface surface,
            string sourceProvenance,
            CancellationToken cancellationToken) =>
            Task.FromResult(CountryPolicyDecision.Allow(new CountryPolicyEvidence(
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
