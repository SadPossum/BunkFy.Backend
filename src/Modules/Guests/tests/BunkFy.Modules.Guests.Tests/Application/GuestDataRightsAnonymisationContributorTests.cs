namespace BunkFy.Modules.Guests.Tests.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Application;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Contributors;
using BunkFy.Modules.Guests.Contracts;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GuestDataRightsAnonymisationContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Contributor_maps_exact_owner_receipt_to_versioned_proof()
    {
        DataRightsAnonymisationContributionRequest request = CreateRequest();
        GuestAnonymisationReceiptDto receipt = CreateReceipt(request);
        FakeDispatcher dispatcher = new(Result.Success(receipt));
        GuestDataRightsAnonymisationContributor contributor = new(
            dispatcher,
            new TestClock());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Completed,
            result.Status);
        DataRightsAnonymisationOwnerProof proof = Assert.IsType<
            DataRightsAnonymisationOwnerProof>(result.OwnerProof);
        Assert.Equal(receipt.ReceiptId, proof.ReceiptId);
        Assert.Equal(receipt.CanonicalSha256, proof.ReceiptSha256);
        ApplyGuestAnonymisationCommand command =
            Assert.IsType<ApplyGuestAnonymisationCommand>(dispatcher.Command);
        Assert.Equal(request.IdempotencyKey, command.IdempotencyKey);
        Assert.Equal(request.RoutingPolicy.PropertyVersion,
            command.RoutingPolicy.PropertyPolicySourceVersion);
    }

    [Fact]
    public async Task Stable_guest_blocker_remains_a_blocked_contribution()
    {
        DataRightsAnonymisationContributionRequest request = CreateRequest();
        Error blocker = GuestsApplicationErrors.AnonymisationBlocked(
            GuestAnonymisationBlockerCode.ActiveDataHold);
        FakeDispatcher dispatcher = new(
            Result.Failure<GuestAnonymisationReceiptDto>(blocker));
        GuestDataRightsAnonymisationContributor contributor = new(
            dispatcher,
            new TestClock());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Blocked,
            result.Status);
        Assert.Equal(blocker.Code, result.OutcomeCode);
        Assert.Null(result.OwnerProof);
    }

    [Fact]
    public async Task Mismatched_receipt_fails_closed()
    {
        DataRightsAnonymisationContributionRequest request = CreateRequest();
        FakeDispatcher dispatcher = new(Result.Success(
            CreateReceipt(request) with { GuestId = Guid.NewGuid() }));
        GuestDataRightsAnonymisationContributor contributor = new(
            dispatcher,
            new TestClock());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(request, CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Failed,
            result.Status);
        Assert.Equal(
            GuestsApplicationErrors.AnonymisationProofUnavailable.Code,
            result.OutcomeCode);
    }

    private static DataRightsAnonymisationContributionRequest CreateRequest()
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
            new DataRightsSubjectCoordinate(
                GuestsDataRightsCoordinates.Owner,
                GuestsDataRightsCoordinates.GuestProfileRecordType,
                Guid.NewGuid(),
                RecordVersion: 3),
            new DataRightsApprovalEvidence(
                SchemaVersion: 1,
                propertyId,
                PropertyVersion: 11,
                "GB",
                "approved-policy",
                PolicyVersion: 3,
                "guest-retention",
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

    private static GuestAnonymisationReceiptDto CreateReceipt(
        DataRightsAnonymisationContributionRequest request) =>
        new(
            ContractVersion: 1,
            Guid.NewGuid(),
            request.IdempotencyKey,
            request.RoutingPropertyId,
            request.CaseId,
            request.ApprovalRevision,
            request.OperationRevision,
            request.Coordinate.RecordId,
            request.Coordinate.RecordVersion,
            ResultingGuestVersion: request.Coordinate.RecordVersion + 1,
            GuestAnonymisationDisposition.Completed,
            GuestAnonymisationReason.ProfileAnonymised,
            AffectedPropertyCount: 1,
            new string('b', 64),
            new string('c', 64),
            Guid.NewGuid(),
            request.ExecutingActorId,
            Now.AddSeconds(30),
            new string('d', 64));

    private sealed class FakeDispatcher(Result<GuestAnonymisationReceiptDto> result)
        : IRequestDispatcher
    {
        public object? Command { get; private set; }

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            this.Command = command;
            return Task.FromResult((Result<TResponse>)(object)result);
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
