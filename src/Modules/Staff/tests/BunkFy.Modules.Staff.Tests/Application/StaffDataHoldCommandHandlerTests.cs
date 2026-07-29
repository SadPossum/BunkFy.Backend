namespace BunkFy.Modules.Staff.Tests.Application;

using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Handlers;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffDataHoldCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Independent_holds_replay_exactly_and_release_independently()
    {
        StaffMember member = CreateMember();
        RecordingHoldRepository holds = new();
        RecordingOperationLock operationLock = new();
        PlaceStaffDataHoldCommandHandler place = new(
            new StubStaffMemberRepository(member),
            holds,
            operationLock,
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        PlaceStaffDataHoldCommand first = new(
            Guid.NewGuid(),
            member.Id,
            member.Version,
            StaffDataHoldReasonCodes.RegulatoryRequest,
            "user:privacy");

        Result<StaffDataHoldReceiptDto> placed =
            await place.HandleAsync(first, CancellationToken.None);
        Result<StaffDataHoldReceiptDto> replay =
            await place.HandleAsync(first, CancellationToken.None);
        Result<StaffDataHoldReceiptDto> changedReplay =
            await place.HandleAsync(
                first with
                {
                    ReasonCode = StaffDataHoldReasonCodes.Dispute
                },
                CancellationToken.None);
        Result<StaffDataHoldReceiptDto> second =
            await place.HandleAsync(
                first with
                {
                    IdempotencyKey = Guid.NewGuid(),
                    ReasonCode =
                        StaffDataHoldReasonCodes.SecurityInvestigation
                },
                CancellationToken.None);

        Assert.True(placed.IsSuccess);
        Assert.Equal(placed.Value, replay.Value);
        Assert.Equal(
            StaffApplicationErrors.DataHoldIdempotencyConflict,
            changedReplay.Error);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, holds.Holds.Count);

        StaffDataHold firstHold = holds.Holds.Single(hold =>
            hold.Id == placed.Value.HoldId);
        ReleaseStaffDataHoldCommandHandler release = new(
            new StubStaffMemberRepository(member),
            holds,
            operationLock,
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        ReleaseStaffDataHoldCommand releaseCommand = new(
            Guid.NewGuid(),
            member.Id,
            firstHold.Id,
            member.Version,
            firstHold.Version,
            "user:decision-maker");
        Result<StaffDataHoldReceiptDto> released =
            await release.HandleAsync(
                releaseCommand,
                CancellationToken.None);
        Result<StaffDataHoldReceiptDto> releaseReplay =
            await release.HandleAsync(
                releaseCommand,
                CancellationToken.None);

        Assert.True(released.IsSuccess);
        Assert.Equal(released.Value, releaseReplay.Value);
        Assert.Equal(StaffDataHoldState.Released, firstHold.State);
        Assert.Single(holds.Holds, hold =>
            hold.State == StaffDataHoldState.Active);
        Assert.Equal(3, operationLock.CallCount);
    }

    [Fact]
    public async Task Invalid_reason_and_stale_staff_leave_no_hold_or_receipt()
    {
        StaffMember member = CreateMember();
        RecordingHoldRepository holds = new();
        PlaceStaffDataHoldCommandHandler handler = new(
            new StubStaffMemberRepository(member),
            holds,
            new RecordingOperationLock(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());
        PlaceStaffDataHoldCommand command = new(
            Guid.NewGuid(),
            member.Id,
            member.Version,
            "free-text-legal-advice",
            "user:privacy");

        Result<StaffDataHoldReceiptDto> invalid =
            await handler.HandleAsync(
                command,
                CancellationToken.None);
        Result<StaffDataHoldReceiptDto> stale =
            await handler.HandleAsync(
                command with
                {
                    IdempotencyKey = Guid.NewGuid(),
                    ReasonCode = StaffDataHoldReasonCodes.Dispute,
                    ExpectedStaffVersion = member.Version + 1
                },
                CancellationToken.None);

        Assert.Equal(
            StaffApplicationErrors.DataHoldRequestInvalid,
            invalid.Error);
        Assert.Equal(
            StaffApplicationErrors.DataHoldStaffVersionConflict,
            stale.Error);
        Assert.Empty(holds.Holds);
        Assert.Empty(holds.Receipts);
    }

    [Fact]
    public async Task Hold_limit_fails_closed_without_writing_a_receipt()
    {
        StaffMember member = CreateMember();
        RecordingHoldRepository holds = new()
        {
            CountOverride =
                StaffDataHold.MaximumRecordsPerStaffMember
        };
        PlaceStaffDataHoldCommandHandler handler = new(
            new StubStaffMemberRepository(member),
            holds,
            new RecordingOperationLock(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

        Result<StaffDataHoldReceiptDto> result =
            await handler.HandleAsync(
                new PlaceStaffDataHoldCommand(
                    Guid.NewGuid(),
                    member.Id,
                    member.Version,
                    StaffDataHoldReasonCodes.LegalObligation,
                    "user:privacy"),
                CancellationToken.None);

        Assert.Equal(
            StaffApplicationErrors.DataHoldLimitReached,
            result.Error);
        Assert.Empty(holds.Holds);
        Assert.Empty(holds.Receipts);
    }

    [Fact]
    public async Task Unknown_lock_coordinate_returns_staff_not_found()
    {
        StaffMember member = CreateMember();
        RecordingHoldRepository holds = new();
        PlaceStaffDataHoldCommandHandler handler = new(
            new StubStaffMemberRepository(member),
            holds,
            new RecordingOperationLock(memberExists: false),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

        Result<StaffDataHoldReceiptDto> result =
            await handler.HandleAsync(
                new PlaceStaffDataHoldCommand(
                    Guid.NewGuid(),
                    member.Id,
                    member.Version,
                    StaffDataHoldReasonCodes.Dispute,
                    "user:privacy"),
                CancellationToken.None);

        Assert.Equal(
            StaffApplicationErrors.StaffMemberNotFound,
            result.Error);
        Assert.Empty(holds.Holds);
        Assert.Empty(holds.Receipts);
    }

    private static StaffMember CreateMember() =>
        StaffMember.Create(
            Guid.NewGuid(),
            "tenant-a",
            "Staff member",
            legalName: null,
            workEmail: null,
            workPhone: null,
            employeeNumber: null,
            jobTitle: null,
            department: null,
            authSubjectId: null,
            "user:creator",
            Guid.NewGuid(),
            Now.AddHours(-1)).Value;

    private sealed class RecordingHoldRepository(
        IEnumerable<StaffDataHold>? initial = null)
        : IStaffDataHoldRepository
    {
        public List<StaffDataHold> Holds { get; } =
            initial?.ToList() ?? [];
        public List<StaffDataHoldReceipt> Receipts { get; } = [];
        public long? CountOverride { get; init; }

        public Task<StaffDataHoldReceipt?>
            FindReceiptByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult(this.Receipts.SingleOrDefault(receipt =>
                receipt.IdempotencyKey == idempotencyKey));

        public Task<StaffDataHold?> GetAsync(
            Guid staffMemberId,
            Guid holdId,
            CancellationToken cancellationToken) =>
            Task.FromResult(this.Holds.SingleOrDefault(hold =>
                hold.StaffMemberId == staffMemberId &&
                hold.Id == holdId));

        public Task<IReadOnlyCollection<StaffDataHold>> ListAsync(
            Guid staffMemberId,
            StaffDataHoldStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<StaffDataHold>>(
                this.Holds
                    .Where(hold =>
                        hold.StaffMemberId == staffMemberId)
                    .Skip(pageRequest.SkipCount)
                    .Take(pageRequest.PageSize)
                    .ToArray());

        public Task<long> CountAsync(
            Guid staffMemberId,
            StaffDataHoldStatus? status,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.CountOverride ??
                this.Holds.LongCount(hold =>
                    hold.StaffMemberId == staffMemberId));

        public Task AddAsync(
            StaffDataHold hold,
            CancellationToken cancellationToken)
        {
            this.Holds.Add(hold);
            return Task.CompletedTask;
        }

        public Task AddReceiptAsync(
            StaffDataHoldReceipt receipt,
            CancellationToken cancellationToken)
        {
            this.Receipts.Add(receipt);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOperationLock(
        bool memberExists = true) : IStaffOperationLock
    {
        public int CallCount { get; private set; }

        public Task<long?> GetStaffMemberRevisionAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult<long?>(memberExists ? 1 : null);

        public Task<bool> TryAcquireStaffMemberAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            return Task.FromResult(memberExists);
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
