namespace BunkFy.Modules.Staff.Tests.Application;

using System.Text.Json;
using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Contributors;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.Aggregates;
using BunkFy.Modules.Staff.Domain.DataRights;
using BunkFy.Modules.Staff.Domain.Governance;
using BunkFy.Modules.Staff.Domain.Models;
using Gma.Framework.Pagination;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffDataRightsAnonymisationPolicyContributorTests
{
    private const string TenantId = "tenant-a";
    private const string PolicyDigest =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly DateTimeOffset Now =
        new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Eligible_departed_staff_returns_bounded_non_pii_evidence()
    {
        Scenario scenario = CreateScenario(Now.AddDays(-2_556));

        DataRightsAnonymisationPolicyContributionResult result =
            await scenario.Contributor.EvaluateAsync(
                Request(scenario.Member),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationPolicyContributionStatus.Approved,
            result.Status);
        DataRightsAnonymisationPolicyContributionEvidence evidence =
            Assert.IsType<DataRightsAnonymisationPolicyContributionEvidence>(
                result.Evidence);
        Assert.Equal("staff-employment", evidence.RetentionDataClass);
        Assert.Equal("employment-ended", evidence.RetentionTrigger);
        Assert.Equal(Now, evidence.EvaluatedAtUtc);
        Assert.True(
            evidence.RetentionDeadlineUtc <= evidence.EvaluatedAtUtc);
        Assert.True(evidence.RequiresDistinctExecutor);
        Assert.Collection(
            evidence.StateBindings.OrderBy(binding => binding.Key),
            binding => AssertBinding(binding, "staff.governance", 1),
            binding => AssertBinding(binding, "staff.holds", 42),
            binding => AssertBinding(binding, "staff.record", 2),
            binding => AssertBinding(binding, "staff.restriction", 0));

        string serialized = JsonSerializer.Serialize(evidence);
        Assert.DoesNotContain(
            "Private Staff Name",
            serialized,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "private.staff@example.test",
            serialized,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Active_data_hold_denies_approval()
    {
        StaffDataHold hold = StaffDataHold.Place(
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid(),
            StaffDataHoldReasonCodes.LegalObligation,
            "user:privacy",
            Now.AddDays(-10)).Value;
        Scenario scenario = CreateScenario(
            Now.AddDays(-2_556),
            holds: [hold]);
        hold = StaffDataHold.Place(
            hold.Id,
            TenantId,
            scenario.Member.Id,
            hold.ReasonCode,
            hold.PlacedBy,
            hold.PlacedAtUtc).Value;
        scenario.Holds.ReplaceWith(hold);

        DataRightsAnonymisationPolicyContributionResult result =
            await scenario.Contributor.EvaluateAsync(
                Request(scenario.Member),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationPolicyContributionStatus.Denied,
            result.Status);
        Assert.Equal(
            "staff.anonymisation-policy.active-hold",
            result.OutcomeCode);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public async Task Retention_period_not_due_denies_approval()
    {
        Scenario scenario = CreateScenario(Now.AddDays(-100));

        DataRightsAnonymisationPolicyContributionResult result =
            await scenario.Contributor.EvaluateAsync(
                Request(scenario.Member),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationPolicyContributionStatus.Denied,
            result.Status);
        Assert.Equal(
            "staff.anonymisation-policy.retention-not-due",
            result.OutcomeCode);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public async Task Concurrent_staff_change_denies_approval()
    {
        Scenario scenario = CreateScenario(
            Now.AddDays(-2_556),
            lockRevisions: [42, 43]);

        DataRightsAnonymisationPolicyContributionResult result =
            await scenario.Contributor.EvaluateAsync(
                Request(scenario.Member),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationPolicyContributionStatus.Denied,
            result.Status);
        Assert.Equal(
            "staff.anonymisation-policy.concurrent-change",
            result.OutcomeCode);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public async Task Stale_staff_coordinate_denies_approval()
    {
        Scenario scenario = CreateScenario(Now.AddDays(-2_556));
        DataRightsAnonymisationPolicyContributionRequest request =
            Request(scenario.Member) with
            {
                Coordinate = Request(scenario.Member).Coordinate with
                {
                    RecordVersion = scenario.Member.Version + 1
                }
            };

        DataRightsAnonymisationPolicyContributionResult result =
            await scenario.Contributor.EvaluateAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationPolicyContributionStatus.Denied,
            result.Status);
        Assert.Equal(
            "staff.anonymisation-policy.lifecycle-denied",
            result.OutcomeCode);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public async Task Governance_for_an_older_staff_version_denies_approval()
    {
        Scenario scenario = CreateScenario(
            Now.AddDays(-2_556),
            governanceStaffVersion: 1);

        DataRightsAnonymisationPolicyContributionResult result =
            await scenario.Contributor.EvaluateAsync(
                Request(scenario.Member),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationPolicyContributionStatus.Denied,
            result.Status);
        Assert.Equal(
            "staff.anonymisation-policy.state-unavailable",
            result.OutcomeCode);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public async Task Cross_tenant_staff_repository_result_fails_closed()
    {
        Scenario scenario = CreateScenario(
            Now.AddDays(-2_556),
            memberTenantId: "tenant-b");

        DataRightsAnonymisationPolicyContributionResult result =
            await scenario.Contributor.EvaluateAsync(
                Request(scenario.Member),
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationPolicyContributionStatus.Denied,
            result.Status);
        Assert.Equal(
            "staff.anonymisation-policy.state-unavailable",
            result.OutcomeCode);
        Assert.Null(result.Evidence);
    }

    private static Scenario CreateScenario(
        DateTimeOffset departedAtUtc,
        IReadOnlyCollection<StaffDataHold>? holds = null,
        IReadOnlyCollection<long?>? lockRevisions = null,
        long? governanceStaffVersion = null,
        string memberTenantId = TenantId)
    {
        StaffMember member = StaffMember.Create(
            Guid.NewGuid(),
            memberTenantId,
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

        StaffEmploymentGovernanceBinding binding =
            StaffEmploymentGovernanceBinding.Create(
                "GB",
                "staff-test",
                1,
                "eu-west-2",
                "uk-no-transfer",
                "staff-employment",
                1,
                PolicyDigest,
                new DateTimeOffset(
                    2026,
                    1,
                    1,
                    0,
                    0,
                    0,
                    TimeSpan.Zero),
                new DateTimeOffset(
                    2027,
                    1,
                    1,
                    0,
                    0,
                    0,
                    TimeSpan.Zero),
                Now.AddMinutes(-2)).Value;
        StaffEmploymentGovernance governance =
            StaffEmploymentGovernance.Configure(
                TenantId,
                member.Id,
                governanceStaffVersion ?? member.Version,
                binding,
                [
                    StaffEmploymentGovernanceAcknowledgement.Create(
                        "operator-notice",
                        1).Value
                ],
                "user:privacy",
                Now.AddMinutes(-1)).Value;
        StaffProcessingRestrictionProjection restriction =
            StaffProcessingRestrictionProjection.Create(
                TenantId,
                member.Id,
                StaffProcessingRestrictionContract.CurrentVersion,
                Now.AddMinutes(-1)).Value;
        StubHoldRepository holdRepository = new(holds);
        StubOperationLock operationLock = new(
            lockRevisions ?? [42, 42]);
        StaffDataRightsAnonymisationPolicyContributor contributor = new(
            new StubStaffMemberRepository(member),
            new StubGovernanceRepository(governance),
            new StubRestrictionRepository(restriction),
            holdRepository,
            operationLock,
            Registry(),
            new TestScopeContext(),
            new TestClock());
        return new(member, contributor, holdRepository);
    }

    private static DataRightsAnonymisationPolicyContributionRequest Request(
        StaffMember member) =>
        new(
            DataRightsAnonymisationPolicyContract.CurrentVersion,
            TenantId,
            DataRightsCaseType.StaffRights,
            PropertyId: null,
            Guid.NewGuid(),
            new(
                StaffDataRightsCoordinates.Owner,
                StaffDataRightsCoordinates.StaffMemberRecordType,
                member.Id,
                member.Version));

    private static void AssertBinding(
        DataRightsApprovalEvidenceBinding binding,
        string expectedKey,
        long expectedVersion)
    {
        Assert.Equal(expectedKey, binding.Key);
        Assert.Equal(expectedVersion, binding.Version);
        Assert.Equal(64, binding.Sha256.Length);
        Assert.All(
            binding.Sha256,
            character => Assert.True(
                character is (>= '0' and <= '9') or
                    (>= 'a' and <= 'f')));
    }

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
                    PurposeCodes = ["staff-employment-governance"]
                }
            ],
            PurposeRules =
            [
                new()
                {
                    PurposeCode = "staff-employment-governance",
                    LegalRuleReferenceKeys = ["employment-governance"],
                    AllowedSurfaces = [CountryPolicySurface.ApiWrite],
                    AllowedSourceProvenance =
                        ["authorized-workspace-operator"]
                },
                new()
                {
                    PurposeCode =
                        StaffDataRightsAnonymisationPolicyContributor
                            .PurposeCode,
                    LegalRuleReferenceKeys = ["data-subject-erasure"],
                    AllowedSurfaces = [CountryPolicySurface.Erasure],
                    AllowedSourceProvenance =
                    [
                        StaffDataRightsAnonymisationPolicyContributor
                            .SourceProvenance
                    ]
                },
                new()
                {
                    PurposeCode =
                        StaffDataRightsAnonymisationPolicyContributor
                            .RetentionPurposeCode,
                    LegalRuleReferenceKeys = ["storage-limitation"],
                    AllowedSurfaces = [CountryPolicySurface.Retention],
                    AllowedSourceProvenance =
                    [
                        StaffDataRightsAnonymisationPolicyContributor
                            .RetentionSourceProvenance
                    ]
                }
            ],
            RetentionRules =
            [
                new()
                {
                    RetentionPolicyId = "staff-employment",
                    RetentionPolicyVersion = 1,
                    DataClass =
                        StaffDataRightsAnonymisationPolicyContributor
                            .RetentionDataClass,
                    Trigger =
                        StaffDataRightsAnonymisationPolicyContributor
                            .RetentionTrigger,
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
        CountryPolicyPackArtifact artifact =
            new(document, PolicyDigest);
        return CountryPolicyRegistry.Create(
            [artifact],
            [
                new(
                    "GB",
                    "staff-test",
                    1,
                    PolicyDigest,
                    CountryLaunchStatus.Approved)
            ],
            CountryPolicyRuntimeMode.Production);
    }

    private sealed record Scenario(
        StaffMember Member,
        StaffDataRightsAnonymisationPolicyContributor Contributor,
        StubHoldRepository Holds);

    private sealed class StubGovernanceRepository(
        StaffEmploymentGovernance governance)
        : IStaffEmploymentGovernanceRepository
    {
        public Task<StaffEmploymentGovernance?> GetAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult<StaffEmploymentGovernance?>(
                governance.StaffMemberId == staffMemberId
                    ? governance
                    : null);

        public Task<StaffEmploymentGovernanceChangeReceipt?>
            FindReceiptByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(
            StaffEmploymentGovernance added,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddReceiptAsync(
            StaffEmploymentGovernanceChangeReceipt receipt,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubRestrictionRepository(
        StaffProcessingRestrictionProjection restriction)
        : IStaffProcessingRestrictionProjectionRepository
    {
        public Task<StaffProcessingRestrictionProjection?> GetAsync(
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult<StaffProcessingRestrictionProjection?>(
                restriction.StaffMemberId == staffMemberId
                    ? restriction
                    : null);
    }

    private sealed class StubHoldRepository(
        IEnumerable<StaffDataHold>? initial = null)
        : IStaffDataHoldRepository
    {
        private readonly List<StaffDataHold> records =
            initial?.ToList() ?? [];

        public void ReplaceWith(StaffDataHold hold)
        {
            this.records.Clear();
            this.records.Add(hold);
        }

        public Task<StaffDataHoldReceipt?>
            FindReceiptByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<StaffDataHold?> GetAsync(
            Guid staffMemberId,
            Guid holdId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<StaffDataHold>> ListAsync(
            Guid staffMemberId,
            StaffDataHoldStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<StaffDataHold>>(
                this.Filter(staffMemberId, status)
                    .Skip(pageRequest.SkipCount)
                    .Take(pageRequest.PageSize)
                    .ToArray());

        public Task<long> CountAsync(
            Guid staffMemberId,
            StaffDataHoldStatus? status,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.Filter(staffMemberId, status).LongCount());

        public Task AddAsync(
            StaffDataHold hold,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddReceiptAsync(
            StaffDataHoldReceipt receipt,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        private IEnumerable<StaffDataHold> Filter(
            Guid staffMemberId,
            StaffDataHoldStatus? status) =>
            this.records.Where(hold =>
                hold.StaffMemberId == staffMemberId &&
                (status is null ||
                    (int)hold.State == (int)status.Value));
    }

    private sealed class StubOperationLock(
        IEnumerable<long?> revisions)
        : IStaffOperationLock
    {
        private readonly Queue<long?> revisions = new(revisions);

        public Task<long?> GetStaffMemberRevisionAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                this.revisions.Count > 0
                    ? this.revisions.Dequeue()
                    : null);

        public Task<bool> TryAcquireStaffMemberAsync(
            string tenantId,
            Guid staffMemberId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => TenantId;
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
