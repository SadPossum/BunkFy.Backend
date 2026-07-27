namespace BunkFy.Modules.Inventory.Tests;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Inventory.Application;
using BunkFy.Modules.Inventory.Application.Commands;
using BunkFy.Modules.Inventory.Application.Contributors;
using BunkFy.Modules.Inventory.Application.Handlers;
using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class
    InventoryDataRightsAnonymisationContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Contributor_maps_exact_receipt_to_owner_proof()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        InventoryAllocationAnonymisationReceiptDto receipt =
            CreateReceipt(request);
        FakeDispatcher dispatcher = new(Result.Success(receipt));
        InventoryDataRightsAnonymisationContributor contributor =
            new(dispatcher, new TestClock());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Completed,
            result.Status);
        DataRightsAnonymisationOwnerProof proof =
            Assert.IsType<DataRightsAnonymisationOwnerProof>(
                result.OwnerProof);
        Assert.Equal(receipt.ReceiptId, proof.ReceiptId);
        Assert.Equal(
            receipt.ResultingAllocationVersion,
            proof.ResultingRecordVersion);
        Assert.Equal(
            receipt.CanonicalSha256,
            proof.ReceiptSha256);
        ApplyInventoryAllocationAnonymisationCommand command =
            Assert.IsType<
                ApplyInventoryAllocationAnonymisationCommand>(
                    dispatcher.Command);
        Assert.Equal(request, command.Request);
    }

    [Fact]
    public async Task Stable_owner_blocker_remains_blocked()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        Error blocker = InventoryApplicationErrors
            .AnonymisationBlocked(
                ApplyInventoryAllocationAnonymisationCommandHandler
                    .ActiveBlocker);
        InventoryDataRightsAnonymisationContributor contributor =
            new(
                new FakeDispatcher(
                    Result.Failure<
                        InventoryAllocationAnonymisationReceiptDto>(
                            blocker)),
                new TestClock());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Blocked,
            result.Status);
        Assert.Equal(blocker.Code, result.OutcomeCode);
    }

    [Fact]
    public async Task Mismatched_receipt_fails_closed()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        InventoryDataRightsAnonymisationContributor contributor =
            new(
                new FakeDispatcher(Result.Success(
                    CreateReceipt(request) with
                    {
                        AllocationId = Guid.NewGuid()
                    })),
                new TestClock());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Failed,
            result.Status);
        Assert.Equal(
            InventoryApplicationErrors
                .AnonymisationProofUnavailable.Code,
            result.OutcomeCode);
    }

    private static DataRightsAnonymisationContributionRequest
        CreateRequest()
    {
        Guid propertyId = Guid.NewGuid();
        return new(
            DataRightsAnonymisationContract.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            propertyId,
            Guid.NewGuid(),
            ApprovalRevision: 6,
            OperationRevision: 7,
            new(
                InventoryDataRightsCoordinates.Owner,
                InventoryDataRightsCoordinates.AllocationRecordType,
                Guid.NewGuid(),
                RecordVersion: 3),
            new(
                SchemaVersion: 1,
                propertyId,
                PropertyVersion: 11,
                "GB",
                "approved-policy",
                PolicyVersion: 3,
                "inventory-retention",
                RetentionPolicyVersion: 2,
                new string('a', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                Now.AddMinutes(-1),
                RequiresDistinctExecutor: true),
            "user:executor",
            Now.AddMinutes(2));
    }

    private static InventoryAllocationAnonymisationReceiptDto
        CreateReceipt(
            DataRightsAnonymisationContributionRequest request) =>
        new(
            ContractVersion: 1,
            Guid.NewGuid(),
            request.WorkItemId,
            request.IdempotencyKey,
            request.RoutingPropertyId,
            request.CaseId,
            request.ApprovalRevision,
            request.OperationRevision,
            request.Coordinate.RecordId,
            request.Coordinate.RecordVersion,
            request.Coordinate.RecordVersion + 1,
            Guid.NewGuid(),
            InventoryAllocationAnonymisationDisposition.Completed,
            InventoryAllocationAnonymisationReason
                .ReservationCorrelationPseudonymised,
            RemovedAmendmentDecisionCount: 2,
            new string('b', 64),
            request.ExecutingActorId,
            Now.AddSeconds(30),
            new string('c', 64));

    private sealed class FakeDispatcher(
        Result<InventoryAllocationAnonymisationReceiptDto> result)
        : IRequestDispatcher
    {
        public object? Command { get; private set; }

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            this.Command = command;
            return Task.FromResult(
                (Result<TResponse>)(object)result);
        }

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
