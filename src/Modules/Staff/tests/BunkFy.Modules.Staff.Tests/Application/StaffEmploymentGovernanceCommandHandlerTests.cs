namespace BunkFy.Modules.Staff.Tests.Application;

using BunkFy.DataGovernance;
using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Handlers;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.Governance;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffEmploymentGovernanceCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Configure_replace_and_equivalent_replay_are_versioned_and_idempotent()
    {
        StaffMember member = CreateMember();
        RecordingGovernanceRepository repository = new();
        RecordingOperationLock operationLock = new();
        ConfigureStaffEmploymentGovernanceCommandHandler handler =
            CreateHandler(member, repository, operationLock);
        ConfigureStaffEmploymentGovernanceCommand first =
            Command(member, expectedGovernanceVersion: 0);

        Result<StaffEmploymentGovernanceChangeReceiptDto> configured =
            await handler.HandleAsync(
                first,
                CancellationToken.None);
        Result<StaffEmploymentGovernanceChangeReceiptDto> replay =
            await handler.HandleAsync(
                first,
                CancellationToken.None);
        Result<StaffEmploymentGovernanceChangeReceiptDto> changedReplay =
            await handler.HandleAsync(
                first with { DataRegionId = "eu-west-3" },
                CancellationToken.None);
        Result<StaffEmploymentGovernanceChangeReceiptDto> replaced =
            await handler.HandleAsync(
                Command(
                    member,
                    expectedGovernanceVersion: 1) with
                {
                    IdempotencyKey = Guid.NewGuid()
                },
                CancellationToken.None);

        Assert.True(configured.IsSuccess);
        Assert.Equal(configured.Value, replay.Value);
        Assert.Equal(
            StaffApplicationErrors
                .EmploymentGovernanceIdempotencyConflict,
            changedReplay.Error);
        Assert.True(replaced.IsSuccess);
        Assert.Equal(2, repository.Governance!.Version);
        Assert.Equal(2, repository.Receipts.Count);
        Assert.Equal(2, operationLock.CallCount);
    }

    [Fact]
    public async Task Stale_staff_and_denied_policy_leave_no_governance_or_receipt()
    {
        StaffMember member = CreateMember();
        RecordingGovernanceRepository staleRepository = new();
        ConfigureStaffEmploymentGovernanceCommandHandler stale =
            CreateHandler(
                member,
                staleRepository,
                new RecordingOperationLock());
        Result<StaffEmploymentGovernanceChangeReceiptDto> staleResult =
            await stale.HandleAsync(
                Command(member, 0) with
                {
                    ExpectedStaffVersion = member.Version + 1
                },
                CancellationToken.None);

        RecordingGovernanceRepository deniedRepository = new();
        ConfigureStaffEmploymentGovernanceCommandHandler denied =
            CreateHandler(
                member,
                deniedRepository,
                new RecordingOperationLock());
        Result<StaffEmploymentGovernanceChangeReceiptDto> deniedResult =
            await denied.HandleAsync(
                Command(member, 0) with
                {
                    AcceptedAcknowledgements = []
                },
                CancellationToken.None);

        Assert.Equal(
            StaffApplicationErrors
                .EmploymentGovernanceStaffVersionConflict,
            staleResult.Error);
        Assert.StartsWith(
            "Staff.EmploymentGovernancePolicy.",
            deniedResult.Error.Code,
            StringComparison.Ordinal);
        Assert.Null(staleRepository.Governance);
        Assert.Null(deniedRepository.Governance);
        Assert.Empty(staleRepository.Receipts);
        Assert.Empty(deniedRepository.Receipts);
    }

    private static ConfigureStaffEmploymentGovernanceCommandHandler
        CreateHandler(
            StaffMember member,
            RecordingGovernanceRepository repository,
            RecordingOperationLock operationLock) =>
        new(
            new StubStaffMemberRepository(member),
            repository,
            operationLock,
            Registry(),
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

    private static ConfigureStaffEmploymentGovernanceCommand Command(
        StaffMember member,
        long expectedGovernanceVersion) =>
        new(
            Guid.NewGuid(),
            member.Id,
            member.Version,
            expectedGovernanceVersion,
            "GB",
            "staff-test",
            1,
            "eu-west-2",
            "uk-no-transfer",
            "staff-employment",
            1,
            [new("operator-notice", 1)],
            "user:privacy");

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

    private static CountryPolicyRegistry Registry()
    {
        CountryPolicyPackDocument document = new()
        {
            SchemaVersion = 1,
            PolicyId = "staff-test",
            PolicyVersion = 1,
            OperatingCountryCode = "GB",
            ApprovalState = CountryPolicyApprovalState.Approved,
            EffectiveAtUtc = new(
                2026,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero),
            ExpiresAtUtc = new(
                2027,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero),
            AccommodationTypes = ["hostel"],
            GuestCategories = ["staff"],
            FieldRules =
            [
                new()
                {
                    FieldPolicyKey = "staff.profile",
                    GuestCategory = "staff",
                    Requirement =
                        CountryPolicyFieldRequirement.Required,
                    PurposeCodes =
                        ["staff-employment-governance"]
                }
            ],
            PurposeRules =
            [
                new()
                {
                    PurposeCode = "staff-employment-governance",
                    LegalRuleReferenceKeys =
                        ["employment-governance"],
                    AllowedSurfaces =
                        [CountryPolicySurface.ApiWrite],
                    AllowedSourceProvenance =
                        ["authorized-workspace-operator"]
                }
            ],
            RetentionRules =
            [
                new()
                {
                    RetentionPolicyId = "staff-employment",
                    RetentionPolicyVersion = 1,
                    DataClass = "staff-employment",
                    Trigger = "employment-ended",
                    Period = "2555.00:00:00"
                }
            ],
            RightsRule = new()
            {
                Registration = "standard-registration",
                Export = "standard-export",
                Correction = "standard-correction",
                Restriction = "standard-restriction",
                Erasure = "review-before-erasure"
            },
            Restrictions = new()
            {
                Minors = "not-applicable",
                Documents = "prohibited",
                SpecialCategoryData = "prohibited"
            },
            PermittedDataRegions = ["eu-west-2"],
            PermittedTransferProfiles = ["uk-no-transfer"],
            RequiredAcknowledgements =
            [
                new()
                {
                    AcknowledgementId = "operator-notice",
                    AcknowledgementVersion = 1
                }
            ],
            Approval = new()
            {
                OwnerReference = "test-owner",
                ReviewerReference = "test-reviewer",
                ReviewedAtUtc = new(
                    2025,
                    12,
                    1,
                    0,
                    0,
                    0,
                    TimeSpan.Zero),
                Sources =
                [
                    new()
                    {
                        ReferenceId = "test-source",
                        Uri = "https://example.test/staff-policy"
                    }
                ],
                DetachedSignatureReference = "test-signature"
            }
        };
        string digest = new('a', 64);
        CountryPolicyPackArtifact artifact = new(document, digest);
        return CountryPolicyRegistry.Create(
            [artifact],
            [
                new(
                    "GB",
                    "staff-test",
                    1,
                    digest,
                    CountryLaunchStatus.Approved)
            ],
            CountryPolicyRuntimeMode.Production);
    }

    private sealed class RecordingGovernanceRepository
        : IStaffEmploymentGovernanceRepository
    {
        public StaffEmploymentGovernance? Governance { get; private set; }
        public List<StaffEmploymentGovernanceChangeReceipt> Receipts
        {
            get;
        } = [];

        public Task<StaffEmploymentGovernance?> GetAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Governance?.StaffMemberId == staffMemberId
                    ? this.Governance
                    : null);

        public Task<StaffEmploymentGovernanceChangeReceipt?>
            FindReceiptByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Receipts.SingleOrDefault(receipt =>
                    receipt.IdempotencyKey == idempotencyKey));

        public Task AddAsync(
            StaffEmploymentGovernance governance,
            CancellationToken cancellationToken)
        {
            this.Governance = governance;
            return Task.CompletedTask;
        }

        public Task AddReceiptAsync(
            StaffEmploymentGovernanceChangeReceipt receipt,
            CancellationToken cancellationToken)
        {
            this.Receipts.Add(receipt);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOperationLock : IStaffOperationLock
    {
        public int CallCount { get; private set; }

        public Task<long?> GetStaffMemberRevisionAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult<long?>(1);

        public Task<bool> TryAcquireStaffMemberAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            return Task.FromResult(true);
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
