namespace BunkFy.Modules.Ingestion.Tests;

using BunkFy.DataGovernance;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.Connections;
using BunkFy.Modules.Ingestion.Domain.Proposals;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Domain.Reprocessing;
using BunkFy.Modules.Ingestion.Domain.Reservations;
using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionAnonymisationEligibilityEvaluatorTests
{
    private const string TenantId = "tenant-a";
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Contract_versions_limits_and_blocker_codes_are_stable()
    {
        Assert.Equal(1, IngestionAnonymisationEligibilityContract.CurrentVersion);
        Assert.Equal(64, IngestionAnonymisationEligibilityContract.Sha256Length);
        Assert.Equal(
            1_000,
            IngestionAnonymisationEligibilityContract.MaximumGraphRecords);
        Assert.Equal(
            Enumerable.Range(0, 32),
            Enum.GetValues<IngestionAnonymisationBlockerCode>()
                .Select(value => (int)value));
    }

    [Fact]
    public async Task Cancelled_source_graph_with_exact_evidence_is_eligible()
    {
        PolicyFixture policy = CreatePolicy();
        IngestionAnonymisationEligibilitySnapshot snapshot =
            Snapshot(policy.Binding);

        IngestionAnonymisationEligibilityResult result =
            await EvaluateAsync(policy, snapshot);

        Assert.Equal(
            IngestionAnonymisationEligibilityStatus.Eligible,
            result.Status);
        Assert.Equal(IngestionAnonymisationBlockerCode.None, result.BlockerCode);
        Assert.Equal(snapshot.SourceLink.Version, result.SourceLinkVersion);
        Assert.Equal(
            snapshot.Property!.RetentionFenceVersion,
            result.RetentionFenceVersion);
        Assert.Equal(snapshot.GraphRecordCount, result.GraphRecordCount);
        Assert.Equal(
            IngestionAnonymisationEligibilityContract.Sha256Length,
            result.PolicyEvidenceSha256?.Length);
        Assert.Equal(
            IngestionAnonymisationEligibilityContract.Sha256Length,
            result.OperationFenceSha256?.Length);
    }

    [Fact]
    public async Task Operation_fence_is_order_independent_and_detects_graph_changes()
    {
        PolicyFixture policy = CreatePolicy();
        IngestionAnonymisationEligibilitySnapshot snapshot =
            Snapshot(policy.Binding);
        IngestionAnonymisationReceiptSnapshot secondReceipt =
            snapshot.Receipts.Single() with
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222")
            };
        IngestionAnonymisationEligibilitySnapshot ordered = snapshot with
        {
            Receipts = [snapshot.Receipts.Single(), secondReceipt]
        };
        IngestionAnonymisationEligibilitySnapshot reversed = snapshot with
        {
            Receipts = [secondReceipt, snapshot.Receipts.Single()]
        };
        IngestionAnonymisationEligibilitySnapshot changed = reversed with
        {
            Receipts =
            [
                secondReceipt with { RawPayloadVersion = 7 },
                snapshot.Receipts.Single()
            ]
        };

        IngestionAnonymisationEligibilityResult orderedResult =
            await EvaluateAsync(policy, ordered);
        IngestionAnonymisationEligibilityResult reversedResult =
            await EvaluateAsync(policy, reversed);
        IngestionAnonymisationEligibilityResult changedResult =
            await EvaluateAsync(policy, changed);

        Assert.Equal(
            orderedResult.OperationFenceSha256,
            reversedResult.OperationFenceSha256);
        Assert.NotEqual(
            orderedResult.OperationFenceSha256,
            changedResult.OperationFenceSha256);
    }

    [Fact]
    public void Operation_fence_covers_owner_policy_and_lifecycle_versions()
    {
        PolicyFixture policy = CreatePolicy();
        IngestionAnonymisationEligibilitySnapshot snapshot =
            Snapshot(policy.Binding);
        IngestionAnonymisationReceiptSnapshot receipt =
            snapshot.Receipts.Single();
        string baseline =
            IngestionAnonymisationOperationFence.ComputeSha256(snapshot);
        IngestionAnonymisationEligibilitySnapshot[] changed =
        [
            snapshot with
            {
                SourceLink = snapshot.SourceLink with
                {
                    Version = snapshot.SourceLink.Version + 1
                }
            },
            snapshot with
            {
                Connection = snapshot.Connection with
                {
                    Version = snapshot.Connection.Version + 1
                }
            },
            snapshot with
            {
                Property = snapshot.Property! with
                {
                    PolicySourceVersion =
                        snapshot.Property!.PolicySourceVersion + 1
                }
            },
            snapshot with
            {
                Property = snapshot.Property! with
                {
                    RetentionFenceVersion =
                        snapshot.Property!.RetentionFenceVersion + 1
                }
            },
            snapshot with
            {
                ActiveLegalHoldCount = 1
            },
            snapshot with
            {
                Receipts =
                [
                    receipt with
                    {
                        RawPayloadVersion = receipt.RawPayloadVersion + 1
                    }
                ]
            },
            snapshot with
            {
                Proposals =
                [
                    new(
                        Guid.NewGuid(),
                        receipt.Id,
                        ChangeProposalState.Applied,
                        Version: 2)
                ]
            },
            snapshot with
            {
                Attempts =
                [
                    new(
                        Guid.NewGuid(),
                        receipt.Id,
                        ObservationReprocessingState.Succeeded,
                        Version: 2)
                ]
            }
        ];

        Assert.All(changed, candidate =>
            Assert.NotEqual(
                baseline,
                IngestionAnonymisationOperationFence.ComputeSha256(candidate)));
    }

    [Fact]
    public async Task Stale_source_link_and_routing_policy_evidence_fail_closed()
    {
        PolicyFixture policy = CreatePolicy();
        IngestionAnonymisationEligibilitySnapshot snapshot =
            Snapshot(policy.Binding);

        IngestionAnonymisationEligibilityResult staleSource =
            await EvaluateAsync(
                policy,
                snapshot,
                Request(snapshot, policy.Binding) with
                {
                    SelectedSourceLinkVersion = snapshot.SourceLink.Version - 1
                });
        IngestionAnonymisationEligibilityResult stalePolicy =
            await EvaluateAsync(
                policy,
                snapshot,
                Request(snapshot, policy.Binding) with
                {
                    RoutingPolicy = RoutePolicy(policy.Binding) with
                    {
                        ContentSha256 = new string('b', 64)
                    }
                });

        Assert.Equal(
            IngestionAnonymisationBlockerCode.SourceLinkVersionChanged,
            staleSource.BlockerCode);
        Assert.Equal(
            IngestionAnonymisationBlockerCode.RoutingPolicyDigestChanged,
            stalePolicy.BlockerCode);
    }

    [Fact]
    public async Task Operational_and_reconciliation_work_fail_closed()
    {
        PolicyFixture policy = CreatePolicy();
        IngestionAnonymisationEligibilitySnapshot baseline =
            Snapshot(policy.Binding);
        IngestionAnonymisationReceiptSnapshot receipt =
            baseline.Receipts.Single();

        (IngestionAnonymisationEligibilitySnapshot Snapshot,
            IngestionAnonymisationBlockerCode Blocker)[] scenarios =
        [
            (
                baseline with { ActiveLegalHoldCount = 1 },
                IngestionAnonymisationBlockerCode.ActiveLegalHold),
            (
                baseline with
                {
                    Receipts =
                    [
                        receipt with
                        {
                            RawPayloadRetentionState =
                                RawPayloadRetentionState.Purging
                        }
                    ]
                },
                IngestionAnonymisationBlockerCode.RawPayloadPurgeInProgress),
            (
                baseline with
                {
                    Receipts =
                    [
                        receipt with
                        {
                            ActiveReprocessingAttemptId = Guid.NewGuid(),
                            ReprocessingReservationExpiresAtUtc =
                                Now.AddMinutes(1)
                        }
                    ]
                },
                IngestionAnonymisationBlockerCode
                    .ReprocessingReservationActive),
            (
                baseline with
                {
                    Attempts =
                    [
                        new(
                            Guid.NewGuid(),
                            receipt.Id,
                            ObservationReprocessingState.Running,
                            Version: 2)
                    ]
                },
                IngestionAnonymisationBlockerCode.ReprocessingAttemptActive),
            (
                baseline with
                {
                    Receipts =
                    [
                        receipt with { State = ObservationReceiptState.Pending }
                    ]
                },
                IngestionAnonymisationBlockerCode
                    .ObservationProcessingInProgress),
            (
                baseline with
                {
                    Proposals =
                    [
                        new(
                            Guid.NewGuid(),
                            receipt.Id,
                            ChangeProposalState.Applying,
                            Version: 3)
                    ]
                },
                IngestionAnonymisationBlockerCode.ChangeProposalInProgress),
            (
                baseline with
                {
                    Dispatches =
                    [
                        new(
                            Guid.NewGuid(),
                            receipt.Id,
                            ReservationDispatchState.Accepted,
                            Version: 3)
                    ]
                },
                IngestionAnonymisationBlockerCode.ReservationDispatchInProgress),
            (
                baseline with
                {
                    SourceLink = baseline.SourceLink with
                    {
                        State = ReservationSourceLinkState.Linked
                    }
                },
                IngestionAnonymisationBlockerCode.ProviderReconciliationRequired)
        ];

        foreach ((IngestionAnonymisationEligibilitySnapshot scenario,
                     IngestionAnonymisationBlockerCode expected) in scenarios)
        {
            IngestionAnonymisationEligibilityResult result =
                await EvaluateAsync(policy, scenario);

            Assert.Equal(IngestionAnonymisationEligibilityStatus.Blocked, result.Status);
            Assert.Equal(expected, result.BlockerCode);
            Assert.Null(result.OperationFenceSha256);
        }
    }

    [Theory]
    [InlineData(
        (int)IngestionAnonymisationEligibilityLoadStatus.NotFound,
        IngestionAnonymisationBlockerCode.SourceLinkNotFound)]
    [InlineData(
        (int)IngestionAnonymisationEligibilityLoadStatus.TooLarge,
        IngestionAnonymisationBlockerCode.OwnerGraphTooLarge)]
    [InlineData(
        (int)IngestionAnonymisationEligibilityLoadStatus.Unavailable,
        IngestionAnonymisationBlockerCode.OwnerGraphUnavailable)]
    public async Task Owner_graph_load_failures_are_stable(
        int loadStatus,
        IngestionAnonymisationBlockerCode expected)
    {
        PolicyFixture policy = CreatePolicy();
        IngestionAnonymisationEligibilitySnapshot snapshot =
            Snapshot(policy.Binding);
        IngestionAnonymisationEligibilityEvaluator evaluator = new(
            new StubEligibilityRepository(new(
                (IngestionAnonymisationEligibilityLoadStatus)loadStatus,
                Snapshot: null)),
            policy.Registry,
            new TestScopeContext(),
            new TestClock());

        IngestionAnonymisationEligibilityResult result =
            await evaluator.EvaluateAsync(
                Request(snapshot, policy.Binding),
                CancellationToken.None);

        Assert.Equal(expected, result.BlockerCode);
        Assert.Null(result.OperationFenceSha256);
    }

    [Fact]
    public async Task Tenant_mismatch_is_rejected_before_owner_data_is_loaded()
    {
        PolicyFixture policy = CreatePolicy();
        IngestionAnonymisationEligibilitySnapshot snapshot =
            Snapshot(policy.Binding);
        StubEligibilityRepository repository = new(
            IngestionAnonymisationEligibilityLoadResult.Found(snapshot));
        IngestionAnonymisationEligibilityEvaluator evaluator = new(
            repository,
            policy.Registry,
            new TestScopeContext(),
            new TestClock());

        IngestionAnonymisationEligibilityResult result =
            await evaluator.EvaluateAsync(
                Request(snapshot, policy.Binding) with
                {
                    TenantId = "tenant-b"
                },
                CancellationToken.None);

        Assert.Equal(
            IngestionAnonymisationBlockerCode.TenantMismatch,
            result.BlockerCode);
        Assert.Equal(0, repository.LoadCount);
    }

    [Fact]
    public void Application_registers_data_rights_contributors_once()
    {
        ServiceCollection services = [];

        services.AddIngestionApplication();

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(IIngestionAnonymisationEligibilityEvaluator));
        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(IDataRightsAnonymisationContributor));
        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType ==
                typeof(IDataRightsAnonymisationRestoreContributor));
    }

    private static Task<IngestionAnonymisationEligibilityResult> EvaluateAsync(
        PolicyFixture policy,
        IngestionAnonymisationEligibilitySnapshot snapshot,
        IngestionAnonymisationEligibilityRequest? request = null)
    {
        IngestionAnonymisationEligibilityEvaluator evaluator = new(
            new StubEligibilityRepository(
                IngestionAnonymisationEligibilityLoadResult.Found(snapshot)),
            policy.Registry,
            new TestScopeContext(),
            new TestClock());
        return evaluator.EvaluateAsync(
            request ?? Request(snapshot, policy.Binding),
            CancellationToken.None);
    }

    private static IngestionAnonymisationEligibilitySnapshot Snapshot(
        PropertyGovernancePolicyBinding policy)
    {
        Guid propertyId =
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid connectionId =
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        Guid receiptId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");
        return new(
            new(
                Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                propertyId,
                connectionId,
                Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                ReservationSourceLinkState.Cancelled,
                receiptId,
                receiptId,
                ActiveProductOperationId: null,
                DeferredReceiptId: null,
                Version: 4),
            new(AdapterConnectionState.Enabled, Version: 3),
            new(
                IsKnown: true,
                IsActive: true,
                PropertyProcessingStatus.Enabled,
                TopologySourceVersion: 7,
                PolicySourceVersion: 5,
                RetentionFenceVersion: 6,
                policy),
            ActiveLegalHoldCount: 0,
            Receipts:
            [
                new(
                    receiptId,
                    ObservationReceiptState.Processed,
                    RawPayloadRetentionState.Available,
                    RawPayloadVersion: 2,
                    ActiveReprocessingAttemptId: null,
                    ReprocessingReservationExpiresAtUtc: null,
                    SourceReceiptId: null,
                    ReprocessingAttemptId: null)
            ],
            Proposals: [],
            Dispatches: [],
            Attempts: [],
            Outputs: []);
    }

    private static IngestionAnonymisationEligibilityRequest Request(
        IngestionAnonymisationEligibilitySnapshot snapshot,
        PropertyGovernancePolicyBinding policy) =>
        new(
            IngestionAnonymisationEligibilityContract.CurrentVersion,
            TenantId,
            Guid.NewGuid(),
            ApprovalRevision: 4,
            OperationRevision: 5,
            snapshot.SourceLink.PropertyId,
            snapshot.SourceLink.Id,
            snapshot.SourceLink.Version,
            RoutePolicy(policy));

    private static IngestionAnonymisationRoutingPolicyEvidence RoutePolicy(
        PropertyGovernancePolicyBinding policy) =>
        new(
            PropertyPolicySourceVersion: 5,
            policy.OperatingCountryCode,
            policy.PolicyId,
            policy.PolicyVersion,
            policy.RetentionPolicyId,
            policy.RetentionPolicyVersion,
            policy.ContentSha256,
            "data-rights-anonymisation",
            "erasure",
            "authorized-workspace-operator",
            Now.AddMinutes(-1));

    private static PolicyFixture CreatePolicy()
    {
        CountryPolicyPackDocument document = new()
        {
            SchemaVersion = 1,
            PolicyId = "gb-hostel",
            PolicyVersion = 1,
            OperatingCountryCode = "GB",
            ApprovalState = CountryPolicyApprovalState.Approved,
            EffectiveAtUtc = Now.AddDays(-1),
            ExpiresAtUtc = Now.AddDays(30),
            AccommodationTypes = ["hostel"],
            GuestCategories = ["ordinary-guest"],
            FieldRules =
            [
                new()
                {
                    FieldPolicyKey = "ingestion.source-reference",
                    GuestCategory = "ordinary-guest",
                    Requirement = CountryPolicyFieldRequirement.Optional,
                    PurposeCodes = ["data-rights-anonymisation"]
                }
            ],
            PurposeRules =
            [
                new()
                {
                    PurposeCode = "data-rights-anonymisation",
                    LegalRuleReferenceKeys = ["approved-erasure"],
                    AllowedSurfaces = [CountryPolicySurface.Erasure],
                    AllowedSourceProvenance =
                        ["authorized-workspace-operator"]
                }
            ],
            RetentionRules =
            [
                new()
                {
                    RetentionPolicyId = "ingestion-operational",
                    RetentionPolicyVersion = 1,
                    DataClass = "ingestion-operational",
                    Trigger = "provider-link-terminal",
                    Period = "365.00:00:00"
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
                Minors = "not-assessed",
                Documents = "document-images-prohibited",
                SpecialCategoryData = "prohibited"
            },
            PermittedDataRegions = ["eu-west-2"],
            PermittedTransferProfiles = ["uk-no-transfer"],
            RequiredAcknowledgements = [],
            Approval = new()
            {
                OwnerReference = "private-owner",
                ReviewerReference = "private-reviewer",
                ReviewedAtUtc = Now.AddDays(-2),
                Sources =
                [
                    new()
                    {
                        ReferenceId = "source-1",
                        Uri = "https://example.test/policy"
                    }
                ],
                DetachedSignatureReference = "signature-1"
            }
        };
        CountryPolicyPackValidator.ValidateAndThrow(document);
        string digest = new('a', 64);
        CountryPolicyRegistry registry = CountryPolicyRegistry.Create(
            [new(document, digest)],
            [
                new(
                    document.OperatingCountryCode,
                    document.PolicyId,
                    document.PolicyVersion,
                    digest,
                    CountryLaunchStatus.Approved)
            ],
            CountryPolicyRuntimeMode.Production);
        PropertyGovernancePolicyBinding binding = new(
            document.OperatingCountryCode,
            document.PolicyId,
            document.PolicyVersion,
            document.PermittedDataRegions.Single(),
            document.PermittedTransferProfiles.Single(),
            document.RetentionRules.Single().RetentionPolicyId,
            document.RetentionRules.Single().RetentionPolicyVersion,
            digest,
            document.EffectiveAtUtc,
            document.ExpiresAtUtc,
            Now.AddHours(-1),
            []);
        return new(registry, binding);
    }

    private sealed record PolicyFixture(
        CountryPolicyRegistry Registry,
        PropertyGovernancePolicyBinding Binding);

    private sealed class StubEligibilityRepository(
        IngestionAnonymisationEligibilityLoadResult result)
        : IIngestionAnonymisationEligibilityRepository
    {
        public int LoadCount { get; private set; }

        public Task<IngestionAnonymisationEligibilityLoadResult> LoadAsync(
            Guid propertyId,
            Guid sourceLinkId,
            CancellationToken cancellationToken)
        {
            this.LoadCount++;
            return Task.FromResult(result);
        }
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
