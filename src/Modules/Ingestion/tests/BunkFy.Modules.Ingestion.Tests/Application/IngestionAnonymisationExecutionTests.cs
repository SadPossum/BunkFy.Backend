namespace BunkFy.Modules.Ingestion.Tests.Application;

using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Contracts.Authorization;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Handlers;
using BunkFy.Modules.Ingestion.Application.Policies;
using BunkFy.Modules.Ingestion.Contracts;
using BunkFy.Modules.Ingestion.Domain.DataRights;
using BunkFy.Modules.Ingestion.Domain.Receipts;
using BunkFy.Modules.Ingestion.Persistence;
using BunkFy.Modules.Ingestion.Persistence.Repositories;
using Gma.Framework.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

[Trait("Category", "Unit")]
public sealed class IngestionAnonymisationExecutionTests
{
    [Fact]
    public async Task Execution_is_staged_retry_safe_and_proves_completion()
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(
                    $"ingestion-anonymisation-execution-" +
                    $"{Guid.NewGuid():N}")
                .Options;
        IngestionAnonymisationRestoreTests.TestScopeContext scope =
            new();
        await using IngestionDbContext dbContext =
            new(options, scope);
        IngestionAnonymisationRestoreTests.SeededGraph seeded =
            await IngestionAnonymisationRestoreTests.SeedAsync(
                dbContext);
        IngestionAnonymisationRestoreTests.TestClock clock =
            new(IngestionAnonymisationRestoreTests.Now.AddHours(1));
        IngestionAnonymisationRestoreTests.TestRawPayloadStore
            rawPayloads = new();
        byte[] rawPayload = Encoding.UTF8.GetBytes(
            string.Concat("{\"guest\":\"", "Maya Chen", "\"}"));
        rawPayloads.Add(
            seeded.Receipt.RawPayloadFileId,
            seeded.Receipt.ConnectionId,
            rawPayload);
        IngestionAnonymisationRestoreRepository repository = new(
            dbContext,
            new IngestionDataRightsEvidenceGraphLoader(dbContext));
        DataRightsApprovalEvidence approval =
            CreateApproval(seeded.PropertyId);
        IngestionAnonymisationRoutingPolicyEvidence routing =
            IngestionAnonymisationPolicyEvidence.FromApproval(
                approval);
        string policySha256 =
            IngestionAnonymisationPolicyEvidence.ComputeSha256(
                routing);
        DataRightsAnonymisationContributionRequest request =
            CreateRequest(seeded, approval, clock.UtcNow);
        StubEligibility eligibility = new(
            Eligible(
                seeded.SourceLink.Version,
                policySha256));
        StubApprovalGate approvalGate = new(approval);
        BeginIngestionAnonymisationCommandHandler begin = new(
            repository,
            TestIngestionSource.Create(dbContext, scope),
            new HmacIngestionAnonymisationFingerprintService(
                Options.Create(
                    IngestionAnonymisationRestoreTests
                        .FingerprintOptions())),
            eligibility,
            approvalGate,
            scope,
            clock,
            new IngestionAnonymisationRestoreTests.TestIds());
        CompleteIngestionAnonymisationCommandHandler complete =
            new(
                repository,
                TestIngestionSource.Create(dbContext, scope),
                rawPayloads,
                scope,
                clock);

        Result<IngestionAnonymisationExecutionStage> started =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationCommand(request),
                CancellationToken.None);

        Assert.True(started.IsSuccess);
        Assert.Single(started.Value.RawPayloads);
        Assert.Null(started.Value.CompletedProof);
        await dbContext.SaveChangesAsync();
        IngestionAnonymisationTombstone tombstone =
            await dbContext.AnonymisationTombstones
                .SingleAsync();
        Assert.Equal(
            IngestionAnonymisationOrigin.LiveExecution,
            tombstone.Origin);
        Assert.Equal(
            tombstone.OwnerReceiptId,
            seeded.Receipt.RawPayloadPurgeClaimId);
        Assert.Equal(
            RawPayloadRetentionState.Purging,
            seeded.Receipt.RawPayloadRetentionState);

        Result<IngestionAnonymisationExecutionStage> resumed =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationCommand(request),
                CancellationToken.None);
        Assert.True(resumed.IsSuccess);
        Assert.Null(resumed.Value.CompletedProof);
        Assert.Equal(started.Value.RawPayloads, resumed.Value.RawPayloads);
        Assert.Equal(1, eligibility.EvaluationCount);
        Assert.Equal(1, approvalGate.EvaluationCount);

        Result<IngestionAnonymisationExecutionProof> premature =
            await complete.HandleAsync(
                new CompleteIngestionAnonymisationCommand(request),
                CancellationToken.None);
        Assert.True(premature.IsFailure);
        Assert.Equal(
            IngestionApplicationErrors
                .AnonymisationRawPayloadDeletionIncomplete,
            premature.Error);

        Assert.True(await rawPayloads.DeleteAsync(
            seeded.Receipt.RawPayloadFileId,
            IngestionAnonymisationRestoreTests.TenantId,
            seeded.Receipt.ConnectionId,
            CancellationToken.None));
        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Result<IngestionAnonymisationExecutionProof> completed =
            await complete.HandleAsync(
                new CompleteIngestionAnonymisationCommand(request),
                CancellationToken.None);

        Assert.True(completed.IsSuccess);
        await dbContext.SaveChangesAsync();
        IngestionAnonymisationReceipt receipt =
            await dbContext.AnonymisationReceipts.SingleAsync();
        Assert.Equal(receipt.Id, completed.Value.ReceiptId);
        Assert.Equal(
            receipt.CanonicalSha256,
            completed.Value.ReceiptSha256);
        Assert.Equal(
            request.Coordinate.RecordVersion + 1,
            completed.Value.ResultingSourceLinkVersion);
        Assert.Equal(
            IngestionAnonymisationTombstoneState.Completed,
            tombstone.State);
        Assert.Equal(
            RawPayloadRetentionState.Purged,
            seeded.Receipt.RawPayloadRetentionState);
        Assert.DoesNotContain(
            "booking-restore-42",
            receipt.CanonicalSha256,
            StringComparison.Ordinal);

        Result<IngestionAnonymisationExecutionStage> replay =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationCommand(request),
                CancellationToken.None);
        Assert.True(replay.IsSuccess);
        Assert.NotNull(replay.Value.CompletedProof);
        Assert.Equal(
            completed.Value,
            replay.Value.CompletedProof);
        Assert.Equal(1, eligibility.EvaluationCount);
        Assert.Equal(1, approvalGate.EvaluationCount);

        Guid ledgerEntryId = Guid.NewGuid();
        DataRightsAnonymisationRestoreRequest restoreRequest = new(
            DataRightsAnonymisationRestoreContract.CurrentVersion,
            IngestionAnonymisationRestoreTests.TenantId,
            ledgerEntryId,
            TenantSequence: 12,
            new string('d', 64),
            seeded.PropertyId,
            IngestionDataRightsCoordinates.Owner,
            IngestionDataRightsCoordinates
                .ReservationSourceLinkRecordType,
            seeded.SourceLink.Id,
            receipt.ContractVersion,
            receipt.Id,
            receipt.CanonicalSha256,
            receipt.ResultingSourceLinkVersion,
            receipt.CompletedAtUtc);
        BeginIngestionAnonymisationRestoreCommandHandler
            beginRestore = new(
                repository,
                TestIngestionSource.Create(dbContext, scope),
                new HmacIngestionAnonymisationFingerprintService(
                    Options.Create(
                        IngestionAnonymisationRestoreTests
                            .FingerprintOptions())),
                scope,
                clock,
                new IngestionAnonymisationRestoreTests.TestIds());
        CompleteIngestionAnonymisationRestoreCommandHandler
            completeRestore = new(
                repository,
                TestIngestionSource.Create(dbContext, scope),
                rawPayloads,
                scope,
                clock);
        Result<IngestionAnonymisationRestoreStage> replayStarted =
            await beginRestore.HandleAsync(
                new BeginIngestionAnonymisationRestoreCommand(
                    restoreRequest),
                CancellationToken.None);
        Assert.True(replayStarted.IsSuccess);
        Assert.True(tombstone.IsProtectedReplayPending);
        string ownerProofSha256 = tombstone.OwnerReceiptSha256;

        clock.UtcNow = clock.UtcNow.AddMinutes(1);
        Result<DataRightsAnonymisationRestoreProof>
            protectedReplay = await completeRestore.HandleAsync(
                new CompleteIngestionAnonymisationRestoreCommand(
                    restoreRequest),
                CancellationToken.None);
        Assert.True(protectedReplay.IsSuccess);
        Assert.False(tombstone.IsProtectedReplayPending);
        Assert.Equal(ledgerEntryId, protectedReplay.Value.LedgerEntryId);
        Assert.Equal(ownerProofSha256, tombstone.OwnerReceiptSha256);

        Result<IngestionAnonymisationExecutionStage> conflict =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationCommand(
                    request with
                    {
                        ExecutingActorId =
                            "user:different-executor"
                    }),
                CancellationToken.None);
        Assert.True(conflict.IsFailure);
        Assert.Equal(
            IngestionApplicationErrors
                .AnonymisationExecutionConflict,
            conflict.Error);
    }

    [Fact]
    public async Task Begin_blocks_when_current_eligibility_changed()
    {
        DbContextOptions<IngestionDbContext> options =
            new DbContextOptionsBuilder<IngestionDbContext>()
                .UseInMemoryDatabase(
                    $"ingestion-anonymisation-blocked-" +
                    $"{Guid.NewGuid():N}")
                .Options;
        IngestionAnonymisationRestoreTests.TestScopeContext scope =
            new();
        await using IngestionDbContext dbContext =
            new(options, scope);
        IngestionAnonymisationRestoreTests.SeededGraph seeded =
            await IngestionAnonymisationRestoreTests.SeedAsync(
                dbContext);
        DataRightsApprovalEvidence approval =
            CreateApproval(seeded.PropertyId);
        IngestionAnonymisationRestoreRepository repository = new(
            dbContext,
            new IngestionDataRightsEvidenceGraphLoader(dbContext));
        BeginIngestionAnonymisationCommandHandler begin = new(
            repository,
            TestIngestionSource.Create(dbContext, scope),
            new HmacIngestionAnonymisationFingerprintService(
                Options.Create(
                    IngestionAnonymisationRestoreTests
                        .FingerprintOptions())),
            new StubEligibility(
                new(
                    IngestionAnonymisationEligibilityContract
                        .CurrentVersion,
                    IngestionAnonymisationEligibilityStatus.Blocked,
                    IngestionAnonymisationBlockerCode.ActiveLegalHold,
                    seeded.SourceLink.Version,
                    RetentionFenceVersion: 1,
                    ActiveLegalHoldCount: 1,
                    GraphRecordCount: 3,
                    PolicyEvidenceSha256: null,
                    OperationFenceSha256: null,
                    IngestionAnonymisationRestoreTests.Now)),
            new StubApprovalGate(approval),
            scope,
            new IngestionAnonymisationRestoreTests.TestClock(
                IngestionAnonymisationRestoreTests.Now.AddHours(1)),
            new IngestionAnonymisationRestoreTests.TestIds());

        Result<IngestionAnonymisationExecutionStage> result =
            await begin.HandleAsync(
                new BeginIngestionAnonymisationCommand(
                    CreateRequest(
                        seeded,
                        approval,
                        IngestionAnonymisationRestoreTests.Now
                            .AddHours(1))),
                CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            "Ingestion.AnonymisationBlocked.ActiveLegalHold",
            result.Error.Code);
        Assert.Empty(dbContext.AnonymisationTombstones);
        Assert.Null(seeded.SourceLink.AnonymisedAtUtc);
        Assert.Null(seeded.Receipt.AnonymisedAtUtc);
    }

    private static DataRightsAnonymisationContributionRequest
        CreateRequest(
            IngestionAnonymisationRestoreTests.SeededGraph seeded,
            DataRightsApprovalEvidence approval,
            DateTimeOffset nowUtc) =>
        new(
            DataRightsAnonymisationContract.CurrentVersion,
            IngestionAnonymisationRestoreTests.TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            seeded.PropertyId,
            Guid.NewGuid(),
            ApprovalRevision: 2,
            OperationRevision: 3,
            new(
                IngestionDataRightsCoordinates.Owner,
                IngestionDataRightsCoordinates
                    .ReservationSourceLinkRecordType,
                seeded.SourceLink.Id,
                seeded.SourceLink.Version),
            approval,
            "user:executor",
            nowUtc.AddHours(1));

    private static DataRightsApprovalEvidence CreateApproval(
        Guid propertyId) =>
        new(
            SchemaVersion: 1,
            propertyId,
            PropertyVersion: 4,
            OperatingCountryCode: "US",
            PolicyId: "us-default",
            PolicyVersion: 1,
            RetentionPolicyId: "standard",
            RetentionPolicyVersion: 1,
            ContentSha256: new string('a', 64),
            PurposeCode:
                IngestionCountryPolicyAdmission
                    .DataRightsAnonymisationPurpose,
            Surface: "erasure",
            SourceProvenance:
                IngestionCountryPolicyAdmission
                    .AuthorizedOperatorProvenance,
            EvaluatedAtUtc:
                IngestionAnonymisationRestoreTests.Now,
            RequiresDistinctExecutor: true);

    private static IngestionAnonymisationEligibilityResult Eligible(
        long sourceLinkVersion,
        string policySha256) =>
        new(
            IngestionAnonymisationEligibilityContract.CurrentVersion,
            IngestionAnonymisationEligibilityStatus.Eligible,
            IngestionAnonymisationBlockerCode.None,
            sourceLinkVersion,
            RetentionFenceVersion: 2,
            ActiveLegalHoldCount: 0,
            GraphRecordCount: 3,
            policySha256,
            new string('b', 64),
            IngestionAnonymisationRestoreTests.Now);

    private sealed class StubEligibility(
        IngestionAnonymisationEligibilityResult result)
                : IIngestionAnonymisationEligibilityEvaluator
    {
        private readonly IngestionAnonymisationEligibilityResult result = result;

        public int EvaluationCount { get; private set; }

        public Task<IngestionAnonymisationEligibilityResult>
            EvaluateAsync(
                IngestionAnonymisationEligibilityRequest request,
                CancellationToken cancellationToken)
        {
            this.EvaluationCount++;
            return Task.FromResult(this.result);
        }
    }

    private sealed class StubApprovalGate(DataRightsApprovalEvidence approval)
                : IDataRightsOperationApprovalGate
    {
        private readonly DataRightsApprovalEvidence approval = approval;

        public int EvaluationCount { get; private set; }

        public Task<DataRightsOperationApprovalResult> EvaluateAsync(
            DataRightsOperationApprovalRequest request,
            CancellationToken cancellationToken)
        {
            this.EvaluationCount++;
            return Task.FromResult(
                DataRightsOperationApprovalResult
                    .ApprovedWithEvidence(this.approval));
        }
    }
}
