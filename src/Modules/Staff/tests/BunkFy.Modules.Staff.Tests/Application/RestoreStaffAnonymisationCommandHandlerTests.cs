namespace BunkFy.Modules.Staff.Tests.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Handlers;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RestoreStaffAnonymisationCommandHandlerTests
{
    private const string TenantId = "tenant-a";

    private static readonly DateTimeOffset OriginallyCompletedAtUtc =
        new(2026, 7, 30, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ReplayedAtUtc =
        OriginallyCompletedAtUtc.AddHours(1);

    [Fact]
    public async Task Restore_re_scrubs_departed_member_and_records_proof()
    {
        StaffMember member = CreateDepartedMember();
        DataRightsAnonymisationRestoreRequestV3 request =
            CreateRequest(member.Id);
        RecordingRestoreRepository repository = new();
        RecordingOperationLock operationLock = new();
        RestoreStaffAnonymisationCommandHandler handler = CreateHandler(
            member,
            repository,
            operationLock,
            new QueueIdGenerator(Guid.NewGuid()));

        Result<StaffAnonymisationRestoreReceipt> result =
            await handler.HandleAsync(
                new RestoreStaffAnonymisationCommand(request),
                CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error.Code);
        Assert.True(member.MatchesAnonymisedState(
            request.ResultingRecordVersion,
            OriginallyCompletedAtUtc));
        Assert.NotNull(repository.Tombstone);
        Assert.True(repository.Tombstone.MatchesRestore(
            member.Id,
            request.LedgerEntryId,
            OriginallyCompletedAtUtc,
            request.OwnerReceiptSha256));
        Assert.Equal(
            repository.Tombstone.Revision,
            result.Value.TombstoneRevision);
        Assert.Equal(ReplayedAtUtc, result.Value.ReplayedAtUtc);
        Assert.Equal(64, result.Value.CanonicalSha256.Length);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(1, operationLock.AcquireCount);
    }

    [Fact]
    public async Task Equivalent_restore_retry_returns_identical_receipt()
    {
        StaffMember member = CreateDepartedMember();
        DataRightsAnonymisationRestoreRequestV3 request =
            CreateRequest(member.Id);
        RecordingRestoreRepository repository = new();
        RecordingOperationLock operationLock = new();
        RestoreStaffAnonymisationCommandHandler handler = CreateHandler(
            member,
            repository,
            operationLock,
            new QueueIdGenerator(Guid.NewGuid()));

        Result<StaffAnonymisationRestoreReceipt> first =
            await handler.HandleAsync(
                new RestoreStaffAnonymisationCommand(request),
                CancellationToken.None);
        Result<StaffAnonymisationRestoreReceipt> replay =
            await handler.HandleAsync(
                new RestoreStaffAnonymisationCommand(request),
                CancellationToken.None);

        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Same(first.Value, replay.Value);
        Assert.Equal(3, member.Version);
        Assert.Equal(1, repository.AddCount);
        Assert.Equal(2, operationLock.AcquireCount);
    }

    [Fact]
    public async Task Existing_receipt_rejects_changed_owner_coordinate()
    {
        StaffMember member = CreateDepartedMember();
        DataRightsAnonymisationRestoreRequestV3 request =
            CreateRequest(member.Id);
        RecordingRestoreRepository repository = new();
        RestoreStaffAnonymisationCommandHandler handler = CreateHandler(
            member,
            repository,
            new RecordingOperationLock(),
            new QueueIdGenerator(Guid.NewGuid()));
        Assert.True((await handler.HandleAsync(
            new RestoreStaffAnonymisationCommand(request),
            CancellationToken.None)).IsSuccess);

        Result<StaffAnonymisationRestoreReceipt> conflict =
            await handler.HandleAsync(
                new RestoreStaffAnonymisationCommand(
                    request with
                    {
                        OwnerReceiptId = Guid.NewGuid()
                    }),
                CancellationToken.None);

        Assert.Equal(
            StaffApplicationErrors.AnonymisationRestoreProofConflict,
            conflict.Error);
        Assert.Equal(3, member.Version);
        Assert.Equal(1, repository.AddCount);
    }

    private static RestoreStaffAnonymisationCommandHandler CreateHandler(
        StaffMember member,
        RecordingRestoreRepository repository,
        RecordingOperationLock operationLock,
        IIdGenerator ids) =>
        new(
            new StubStaffMemberRepository(member),
            operationLock,
            repository,
            new TestScopeContext(),
            new TestClock(),
            ids);

    private static StaffMember CreateDepartedMember()
    {
        DateTimeOffset departedAtUtc =
            OriginallyCompletedAtUtc.AddDays(-30);
        StaffMember member = StaffMember.Create(
            Guid.NewGuid(),
            TenantId,
            "Private Staff Name",
            legalName: "Private Legal Name",
            workEmail: "private.staff@example.test",
            workPhone: "+44 20 1234 5678",
            employeeNumber: "private-employee-number",
            jobTitle: "Private job title",
            department: "Private department",
            authSubjectId: "private-auth-subject",
            "user:creator",
            Guid.NewGuid(),
            departedAtUtc.AddDays(-30)).Value;
        Assert.True(member.Depart(
            DateOnly.FromDateTime(departedAtUtc.UtcDateTime),
            member.Version,
            "user:manager",
            "employment-ended",
            Guid.NewGuid(),
            [],
            departedAtUtc).IsSuccess);
        member.ClearDomainEvents();
        return member;
    }

    private static DataRightsAnonymisationRestoreRequestV3 CreateRequest(
        Guid staffMemberId) =>
        new(
            DataRightsAnonymisationRestoreContractV3.CurrentVersion,
            TenantId,
            Guid.NewGuid(),
            TenantSequence: 1,
            new string('a', 64),
            DataRightsCaseType.StaffRights,
            DataRightsExecutionScopeKind.Tenant,
            RoutingPropertyId: null,
            StaffDataRightsCoordinates.Owner,
            StaffDataRightsCoordinates.StaffMemberRecordType,
            staffMemberId,
            OwnerReceiptContractVersion: 1,
            Guid.NewGuid(),
            new string('b', 64),
            ResultingRecordVersion: 3,
            OriginallyCompletedAtUtc);

    private sealed class RecordingRestoreRepository
        : IStaffAnonymisationRestoreRepository
    {
        public StaffAnonymisationRestoreReceipt? Receipt { get; private set; }
        public StaffAnonymisationTombstone? Tombstone { get; private set; }
        public int AddCount { get; private set; }

        public Task<StaffAnonymisationTombstone?> GetTombstoneAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Tombstone?.Id == staffMemberId
                    ? this.Tombstone
                    : null);

        public Task<StaffAnonymisationRestoreReceipt?> GetReceiptAsync(
            Guid ledgerEntryId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Receipt?.LedgerEntryId == ledgerEntryId
                    ? this.Receipt
                    : null);

        public Task AddAsync(
            StaffAnonymisationRestoreReceipt receipt,
            StaffAnonymisationTombstone? newTombstone,
            CancellationToken cancellationToken)
        {
            this.Receipt = receipt;
            this.Tombstone ??= newTombstone;
            this.AddCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOperationLock : IStaffOperationLock
    {
        public int AcquireCount { get; private set; }

        public Task<long?> GetStaffMemberRevisionAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> TryAcquireStaffMemberAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken)
        {
            this.AcquireCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => ReplayedAtUtc;
    }

    private sealed class QueueIdGenerator(params Guid[] values)
        : IIdGenerator
    {
        private readonly Queue<Guid> ids = new(values);

        public Guid NewId() => this.ids.Dequeue();
    }
}
