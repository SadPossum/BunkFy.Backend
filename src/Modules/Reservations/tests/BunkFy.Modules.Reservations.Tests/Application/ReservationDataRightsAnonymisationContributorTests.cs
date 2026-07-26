namespace BunkFy.Modules.Reservations.Tests.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Reservations.Application;
using BunkFy.Modules.Reservations.Application.Commands;
using BunkFy.Modules.Reservations.Application.Contributors;
using BunkFy.Modules.Reservations.Application.Ports;
using BunkFy.Modules.Reservations.Contracts;
using BunkFy.Modules.Reservations.Domain.Aggregates;
using BunkFy.Modules.Reservations.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class ReservationDataRightsAnonymisationContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Contributor_maps_current_details_and_exact_owner_proof()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        RecordingDispatcher dispatcher = new(
            Result.Success(CreateReceipt(request, detailsRevision: 5)));
        RecordingEligibilityRepository eligibility =
            new(Snapshot(request.Coordinate.RecordVersion, detailsRevision: 5));
        ReservationDataRightsAnonymisationContributor contributor = new(
            dispatcher,
            eligibility,
            new StubAnonymisationRepository(),
            new TestClock());

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
        Assert.Equal(
            request.Coordinate.RecordVersion + 1,
            proof.ResultingRecordVersion);
        ApplyReservationAnonymisationCommand command =
            Assert.IsType<ApplyReservationAnonymisationCommand>(
                dispatcher.Command);
        Assert.Equal(5, command.ExpectedDetailsRevision);
        Assert.Equal(
            request.RoutingPolicy.ContentSha256,
            command.RoutingPolicy.ContentSha256);
        Assert.Equal(1, eligibility.CallCount);
    }

    [Fact]
    public async Task Existing_owner_proof_reuses_its_selected_details_revision()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        ReservationAnonymisationReceipt existing =
            CreateDomainReceipt(request, detailsRevision: 9);
        RecordingDispatcher dispatcher = new(
            Result.Success(CreateReceipt(request, detailsRevision: 9)));
        RecordingEligibilityRepository eligibility =
            new(snapshot: null);
        ReservationDataRightsAnonymisationContributor contributor = new(
            dispatcher,
            eligibility,
            new StubAnonymisationRepository(existing),
            new TestClock());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Completed,
            result.Status);
        Assert.Equal(0, eligibility.CallCount);
        Assert.Equal(
            9,
            Assert.IsType<ApplyReservationAnonymisationCommand>(
                dispatcher.Command).ExpectedDetailsRevision);
    }

    [Fact]
    public async Task Stale_version_and_mismatched_receipt_fail_closed()
    {
        DataRightsAnonymisationContributionRequest request =
            CreateRequest();
        RecordingDispatcher staleDispatcher = new(
            Result.Success(CreateReceipt(request, detailsRevision: 5)));
        ReservationDataRightsAnonymisationContributor staleContributor =
            new(
                staleDispatcher,
                new RecordingEligibilityRepository(
                    Snapshot(
                        request.Coordinate.RecordVersion + 1,
                        detailsRevision: 5)),
                new StubAnonymisationRepository(),
                new TestClock());

        DataRightsAnonymisationContributionResult stale =
            await staleContributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Blocked,
            stale.Status);
        Assert.Null(staleDispatcher.Command);

        RecordingDispatcher mismatchedDispatcher = new(
            Result.Success(
                CreateReceipt(request, detailsRevision: 5) with
                {
                    ReservationId = Guid.NewGuid()
                }));
        ReservationDataRightsAnonymisationContributor mismatchedContributor =
            new(
                mismatchedDispatcher,
                new RecordingEligibilityRepository(
                    Snapshot(
                        request.Coordinate.RecordVersion,
                        detailsRevision: 5)),
                new StubAnonymisationRepository(),
                new TestClock());

        DataRightsAnonymisationContributionResult mismatched =
            await mismatchedContributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Failed,
            mismatched.Status);
        Assert.Equal(
            ReservationsApplicationErrors
                .AnonymisationProofUnavailable.Code,
            mismatched.OutcomeCode);
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
            new DataRightsSubjectCoordinate(
                ReservationsDataRightsCoordinates.Owner,
                ReservationsDataRightsCoordinates.ReservationRecordType,
                Guid.NewGuid(),
                RecordVersion: 3),
            new DataRightsApprovalEvidence(
                SchemaVersion: 1,
                propertyId,
                PropertyVersion: 11,
                "GB",
                "approved-policy",
                PolicyVersion: 3,
                "reservation-retention",
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

    private static ReservationAnonymisationEligibilitySnapshot Snapshot(
        long version,
        long detailsRevision) =>
        new(
            version,
            detailsRevision,
            ReservationState.Cancelled,
            HasPendingAllocationAmendment: false,
            ReservationSource.External,
            HasDirectSourceReference: false,
            ActiveHoldCount: 0,
            ProcessingRestrictionContractVersion: null,
            Property: null);

    private static ReservationAnonymisationReceiptDto CreateReceipt(
        DataRightsAnonymisationContributionRequest request,
        long detailsRevision) =>
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
            request.Coordinate.RecordVersion + 1,
            detailsRevision,
            detailsRevision + 1,
            BunkFy.Modules.Reservations.Contracts
                .ReservationAnonymisationDisposition.Completed,
            BunkFy.Modules.Reservations.Contracts
                .ReservationAnonymisationReason
                .ReservationOwnerDataRedacted,
            RedactedHistoryCount: 1,
            RemovedGuestLinkCount: 0,
            ReducedExternalOperationCount: 0,
            SuppressedReminderCount: 0,
            new string('b', 64),
            request.RoutingPolicy.ContentSha256,
            Guid.NewGuid(),
            request.ExecutingActorId,
            Now.AddSeconds(30),
            new string('c', 64));

    private static ReservationAnonymisationReceipt CreateDomainReceipt(
        DataRightsAnonymisationContributionRequest request,
        long detailsRevision) =>
        ReservationAnonymisationReceipt.Create(
            Guid.NewGuid(),
            request.TenantId,
            request.IdempotencyKey,
            request.RoutingPropertyId,
            request.CaseId,
            request.ApprovalRevision,
            request.OperationRevision,
            request.Coordinate.RecordId,
            new ReservationAnonymisationOutcome(
                request.Coordinate.RecordVersion,
                request.Coordinate.RecordVersion + 1,
                detailsRevision,
                detailsRevision + 1,
                RemovedGuestLinkCount: 0,
                Guid.NewGuid(),
                request.ExecutingActorId,
                [nameof(Reservation.IsAnonymised)],
                Now.AddSeconds(30)),
            redactedHistoryCount: 1,
            reducedExternalOperationCount: 0,
            suppressedReminderCount: 0,
            new string('b', 64),
            request.RoutingPolicy.ContentSha256).Value;

    private sealed class RecordingDispatcher(object result)
        : IRequestDispatcher
    {
        public object? Command { get; private set; }

        public Task<Result<TResponse>> SendAsync<TResponse>(
            ICommand<TResponse> command,
            CancellationToken cancellationToken = default)
        {
            this.Command = command;
            return Task.FromResult((Result<TResponse>)result);
        }

        public Task<Result<TResponse>> QueryAsync<TResponse>(
            IQuery<TResponse> query,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingEligibilityRepository(
        ReservationAnonymisationEligibilitySnapshot? snapshot)
        : IReservationAnonymisationEligibilityRepository
    {
        public int CallCount { get; private set; }

        public Task<ReservationAnonymisationEligibilitySnapshot?> LoadAsync(
            Guid propertyId,
            Guid reservationId,
            CancellationToken cancellationToken)
        {
            this.CallCount++;
            return Task.FromResult(snapshot);
        }
    }

    private sealed class StubAnonymisationRepository(
        ReservationAnonymisationReceipt? receipt = null)
        : IReservationAnonymisationRepository
    {
        public Task<ReservationAnonymisationReceipt?>
            FindReceiptByIdempotencyKeyAsync(
                Guid idempotencyKey,
                CancellationToken cancellationToken) =>
            Task.FromResult(
                receipt?.IdempotencyKey == idempotencyKey
                    ? receipt
                    : null);

        public Task<ReservationAnonymisationAffectedRecords>
            RedactOwnedRecordsAsync(
                Reservation reservation,
                ReservationAnonymisationOutcome outcome,
                CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> VerifyOwnerStateAsync(
            ReservationAnonymisationReceipt value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddOwnerProofAsync(
            ReservationAnonymisationReceipt value,
            ReservationAnonymisationTombstone tombstone,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }
}
