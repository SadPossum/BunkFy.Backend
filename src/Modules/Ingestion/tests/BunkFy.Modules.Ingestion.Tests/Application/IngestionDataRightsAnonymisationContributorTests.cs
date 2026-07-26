namespace BunkFy.Modules.Ingestion.Tests.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Ingestion.Application;
using BunkFy.Modules.Ingestion.Application.Commands;
using BunkFy.Modules.Ingestion.Application.Contributors;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    IngestionDataRightsAnonymisationContributorTests
{
    [Fact]
    public async Task Contributor_deletes_plan_and_maps_exact_owner_proof()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        Guid fileId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        Guid receiptId = Guid.NewGuid();
        IngestionAnonymisationExecutionProof proof = new(
            ReceiptContractVersion: 1,
            receiptId,
            request.Coordinate.RecordVersion + 1,
            new string('a', 64),
            IngestionAnonymisationRestoreTests.Now.AddMinutes(1));
        RecordingDispatcher dispatcher = new(
            Result.Success(
                new IngestionAnonymisationExecutionStage(
                    request.Coordinate.RecordId,
                    [
                        new IngestionRawPayloadDeletion(
                            fileId,
                            connectionId)
                    ],
                    CompletedProof: null)),
            Result.Success(proof));
        RecordingRawPayloadStore rawPayloads =
            new(fileId, connectionId);
        IngestionDataRightsAnonymisationContributor contributor =
            new(dispatcher, rawPayloads);

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Completed,
            result.Status);
        DataRightsAnonymisationOwnerProof ownerProof =
            Assert.IsType<DataRightsAnonymisationOwnerProof>(
                result.OwnerProof);
        Assert.Equal(receiptId, ownerProof.ReceiptId);
        Assert.Equal(
            "ingestion.completed",
            ownerProof.DispositionCode);
        Assert.Equal(
            "ingestion.provider-evidence-anonymised",
            ownerProof.ReasonCode);
        Assert.Equal(proof.ReceiptSha256, ownerProof.ReceiptSha256);
        Assert.Equal(1, rawPayloads.DeleteCount);
        Assert.Equal(1, rawPayloads.ReadCount);
        Assert.Collection(
            dispatcher.Commands,
            command =>
                Assert.IsType<
                    BeginIngestionAnonymisationCommand>(command),
            command =>
                Assert.IsType<
                    CompleteIngestionAnonymisationCommand>(command));
    }

    [Fact]
    public async Task Contributor_preserves_stable_eligibility_blocker()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        Error blocked =
            IngestionApplicationErrors.AnonymisationBlocked(
                IngestionAnonymisationBlockerCode.ActiveLegalHold);
        RecordingDispatcher dispatcher = new(
            Result.Failure<IngestionAnonymisationExecutionStage>(
                blocked),
            complete: null);
        IngestionDataRightsAnonymisationContributor contributor =
            new(dispatcher, new RecordingRawPayloadStore());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Blocked,
            result.Status);
        Assert.Equal(blocked.Code, result.OutcomeCode);
        Assert.Null(result.OwnerProof);
        Assert.Single(dispatcher.Commands);
    }

    [Fact]
    public async Task Contributor_returns_existing_proof_without_deletion()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        IngestionAnonymisationExecutionProof proof = new(
            ReceiptContractVersion: 1,
            Guid.NewGuid(),
            request.Coordinate.RecordVersion + 1,
            new string('a', 64),
            IngestionAnonymisationRestoreTests.Now.AddMinutes(1));
        RecordingDispatcher dispatcher = new(
            Result.Success(
                new IngestionAnonymisationExecutionStage(
                    request.Coordinate.RecordId,
                    RawPayloads: [],
                    proof)),
            complete: null);
        RecordingRawPayloadStore rawPayloads = new();
        IngestionDataRightsAnonymisationContributor contributor =
            new(dispatcher, rawPayloads);

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Completed,
            result.Status);
        Assert.Equal(proof.ReceiptId, result.OwnerProof?.ReceiptId);
        Assert.Equal(0, rawPayloads.DeleteCount);
        Assert.Equal(0, rawPayloads.ReadCount);
        Assert.Single(dispatcher.Commands);
    }

    [Fact]
    public async Task Contributor_does_not_finalize_while_payload_remains()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        Guid fileId = Guid.NewGuid();
        Guid connectionId = Guid.NewGuid();
        RecordingDispatcher dispatcher = new(
            Result.Success(
                new IngestionAnonymisationExecutionStage(
                    request.Coordinate.RecordId,
                    [
                        new IngestionRawPayloadDeletion(
                            fileId,
                            connectionId)
                    ],
                    CompletedProof: null)),
            complete: null);
        RecordingRawPayloadStore rawPayloads =
            new(fileId, connectionId, deleteEnabled: false);
        IngestionDataRightsAnonymisationContributor contributor =
            new(dispatcher, rawPayloads);

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Failed,
            result.Status);
        Assert.Equal(
            IngestionApplicationErrors
                .AnonymisationRawPayloadDeletionIncomplete.Code,
            result.OutcomeCode);
        Assert.Null(result.OwnerProof);
        Assert.Equal(1, rawPayloads.DeleteCount);
        Assert.Equal(1, rawPayloads.ReadCount);
        Assert.Single(dispatcher.Commands);
    }

    private static DataRightsAnonymisationContributionRequest
        CreateRequest()
    {
        Guid propertyId = Guid.NewGuid();
        return new(
            DataRightsAnonymisationContract.CurrentVersion,
            IngestionAnonymisationRestoreTests.TenantId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            propertyId,
            Guid.NewGuid(),
            ApprovalRevision: 2,
            OperationRevision: 3,
            new(
                IngestionDataRightsCoordinates.Owner,
                IngestionDataRightsCoordinates
                    .ReservationSourceLinkRecordType,
                Guid.NewGuid(),
                RecordVersion: 4),
            new(
                SchemaVersion: 1,
                propertyId,
                PropertyVersion: 4,
                OperatingCountryCode: "US",
                PolicyId: "us-default",
                PolicyVersion: 1,
                RetentionPolicyId: "standard",
                RetentionPolicyVersion: 1,
                ContentSha256: new string('b', 64),
                PurposeCode: "data-rights-anonymisation",
                Surface: "erasure",
                SourceProvenance:
                    "authorized-workspace-operator",
                EvaluatedAtUtc:
                    IngestionAnonymisationRestoreTests.Now,
                RequiresDistinctExecutor: true),
            "user:executor",
            IngestionAnonymisationRestoreTests.Now.AddHours(1));
    }

    private sealed class RecordingDispatcher(
        object begin,
        object? complete)
        : IRequestDispatcher
    {
        public List<object> Commands { get; } = [];

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            this.Commands.Add(command);
            object result = command switch
            {
                BeginIngestionAnonymisationCommand => begin,
                CompleteIngestionAnonymisationCommand
                    when complete is not null => complete,
                _ => throw new NotSupportedException()
            };
            return Task.FromResult((Result<TResponse>)result);
        }

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingRawPayloadStore
        : IRawPayloadStore
    {
        private readonly HashSet<
            (Guid FileId, Guid ConnectionId)> payloads = [];
        private readonly bool deleteEnabled;

        public RecordingRawPayloadStore(
            Guid fileId,
            Guid connectionId,
            bool deleteEnabled = true)
        {
            this.deleteEnabled = deleteEnabled;
            this.payloads.Add((fileId, connectionId));
        }

        public RecordingRawPayloadStore() => this.deleteEnabled = true;

        public int DeleteCount { get; private set; }
        public int ReadCount { get; private set; }

        public Task StoreAsync(
            RawPayloadWrite write,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<RawPayloadRead?> ReadAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken)
        {
            this.ReadCount++;
            return Task.FromResult(
                this.payloads.Contains(
                    (payloadId, connectionId))
                    ? new RawPayloadRead(
                        "application/json",
                        ReadOnlyMemory<byte>.Empty,
                        new string('0', 64))
                    : null);
        }

        public Task<bool> DeleteAsync(
            Guid payloadId,
            string scopeId,
            Guid connectionId,
            CancellationToken cancellationToken)
        {
            this.DeleteCount++;
            return Task.FromResult(
                this.deleteEnabled &&
                this.payloads.Remove(
                    (payloadId, connectionId)));
        }
    }
}
