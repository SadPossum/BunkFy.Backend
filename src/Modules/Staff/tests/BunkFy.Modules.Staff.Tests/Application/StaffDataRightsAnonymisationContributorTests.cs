namespace BunkFy.Modules.Staff.Tests.Application;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Contributors;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class StaffDataRightsAnonymisationContributorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 30, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Contributor_maps_exact_v2_owner_receipt_to_versioned_proof()
    {
        DataRightsAnonymisationContributionRequestV2 request =
            CreateRequest();
        StaffAnonymisationReceiptDto receipt = CreateReceipt(request);
        FakeDispatcher dispatcher = new(Result.Success(receipt));
        StaffDataRightsAnonymisationContributor contributor = new(
            dispatcher,
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
        Assert.Equal(receipt.ReceiptId, proof.ReceiptId);
        Assert.Equal(receipt.CanonicalSha256, proof.ReceiptSha256);
        Assert.Equal(receipt.ResultingStaffVersion,
            proof.ResultingRecordVersion);
        ApplyStaffAnonymisationCommand command =
            Assert.IsType<ApplyStaffAnonymisationCommand>(
                dispatcher.Command);
        Assert.Equal(request.IdempotencyKey, command.IdempotencyKey);
        Assert.Equal(request.Coordinate.RecordId,
            command.StaffMemberId);
        Assert.Equal(request.ApprovalEvidence,
            command.ApprovalEvidence);
    }

    [Fact]
    public async Task Stable_staff_blocker_remains_a_blocked_contribution()
    {
        DataRightsAnonymisationContributionRequestV2 request =
            CreateRequest();
        FakeDispatcher dispatcher = new(
            Result.Failure<StaffAnonymisationReceiptDto>(
                StaffApplicationErrors.AnonymisationStateChanged));
        StaffDataRightsAnonymisationContributor contributor = new(
            dispatcher,
            new TestClock());

        DataRightsAnonymisationContributionResult result =
            await contributor.ExecuteAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Blocked,
            result.Status);
        Assert.Equal(
            StaffApplicationErrors.AnonymisationStateChanged.Code,
            result.OutcomeCode);
        Assert.Null(result.OwnerProof);
    }

    [Fact]
    public async Task Mismatched_receipt_or_non_tenant_request_fails_closed()
    {
        DataRightsAnonymisationContributionRequestV2 request =
            CreateRequest();
        FakeDispatcher mismatchDispatcher = new(Result.Success(
            CreateReceipt(request) with
            {
                StaffMemberId = Guid.NewGuid()
            }));
        StaffDataRightsAnonymisationContributor mismatchContributor =
            new(mismatchDispatcher, new TestClock());

        DataRightsAnonymisationContributionResult mismatch =
            await mismatchContributor.ExecuteAsync(
                request,
                CancellationToken.None);
        DataRightsAnonymisationContributionResult invalidScope =
            await mismatchContributor.ExecuteAsync(
                request with
                {
                    ScopeKind =
                        DataRightsExecutionScopeKind.Property,
                    PropertyId = Guid.NewGuid()
                },
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Failed,
            mismatch.Status);
        Assert.Equal(
            StaffApplicationErrors.AnonymisationProofUnavailable.Code,
            mismatch.OutcomeCode);
        Assert.Equal(
            DataRightsAnonymisationContributionStatus.Failed,
            invalidScope.Status);
        Assert.Equal(
            StaffApplicationErrors.AnonymisationRequestInvalid.Code,
            invalidScope.OutcomeCode);
    }

    [Fact]
    public async Task Restore_contributor_maps_v3_receipt_to_exact_owner_proof()
    {
        DataRightsAnonymisationRestoreRequestV3 request =
            CreateRestoreRequest();
        StaffAnonymisationRestoreReceipt receipt =
            StaffAnonymisationRestoreReceipt.Create(
                request.TenantId,
                request.LedgerEntryId,
                request.RecordId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingRecordVersion,
                tombstoneRevision: 2,
                Now).Value;
        FakeRestoreDispatcher dispatcher = new(Result.Success(receipt));
        StaffDataRightsAnonymisationRestoreContributor contributor =
            new(dispatcher);

        DataRightsAnonymisationRestoreResult result =
            await contributor.RestoreAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationRestoreContractV3.CurrentVersion,
            result.ContractVersion);
        Assert.Equal(
            DataRightsAnonymisationRestoreStatus.Completed,
            result.Status);
        DataRightsAnonymisationRestoreProof proof =
            Assert.IsType<DataRightsAnonymisationRestoreProof>(
                result.Proof);
        Assert.Equal(receipt.LedgerEntryId, proof.LedgerEntryId);
        Assert.Equal(receipt.OwnerReceiptId, proof.OwnerReceiptId);
        Assert.Equal(
            receipt.OwnerReceiptSha256,
            proof.OwnerReceiptSha256);
        Assert.Equal(
            receipt.ResultingStaffVersion,
            proof.ResultingRecordVersion);
        Assert.Equal(
            receipt.TombstoneRevision,
            proof.TombstoneRevision);
        Assert.Equal(receipt.ReplayedAtUtc, proof.ReplayedAtUtc);
        RestoreStaffAnonymisationCommand command =
            Assert.IsType<RestoreStaffAnonymisationCommand>(
                dispatcher.Command);
        Assert.Same(request, command.Request);
    }

    [Fact]
    public async Task Restore_contributor_maps_owner_failure_without_a_proof()
    {
        DataRightsAnonymisationRestoreRequestV3 request =
            CreateRestoreRequest();
        FakeRestoreDispatcher dispatcher = new(
            Result.Failure<StaffAnonymisationRestoreReceipt>(
                StaffApplicationErrors
                    .AnonymisationRestoreProofConflict));
        StaffDataRightsAnonymisationRestoreContributor contributor =
            new(dispatcher);

        DataRightsAnonymisationRestoreResult result =
            await contributor.RestoreAsync(
                request,
                CancellationToken.None);

        Assert.Equal(
            DataRightsAnonymisationRestoreContractV3.CurrentVersion,
            result.ContractVersion);
        Assert.Equal(
            DataRightsAnonymisationRestoreStatus.Failed,
            result.Status);
        Assert.Equal(
            StaffApplicationErrors
                .AnonymisationRestoreProofConflict.Code,
            result.OutcomeCode);
        Assert.Null(result.Proof);
    }

    private static DataRightsAnonymisationContributionRequestV2
        CreateRequest() =>
        new(
            DataRightsAnonymisationContractV2.CurrentVersion,
            "tenant-a",
            DataRightsCaseType.StaffRights,
            DataRightsExecutionScopeKind.Tenant,
            PropertyId: null,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            ApprovalRevision: 6,
            OperationRevision: 7,
            new DataRightsSubjectCoordinate(
                StaffDataRightsCoordinates.Owner,
                StaffDataRightsCoordinates.StaffMemberRecordType,
                Guid.NewGuid(),
                RecordVersion: 3),
            CreateApprovalEvidence(),
            "user:executor",
            Now.AddMinutes(2));

    private static DataRightsApprovalEvidence
        CreateApprovalEvidence() =>
        new(
            SchemaVersion: 2,
            PropertyId: null,
            PropertyVersion: 0,
            OperatingCountryCode: "GB",
            PolicyId: "approved-policy",
            PolicyVersion: 3,
            RetentionPolicyId: "staff-employment",
            RetentionPolicyVersion: 2,
            ContentSha256: new string('a', 64),
            PurposeCode: "staff-data-rights-anonymisation",
            Surface: "erasure",
            SourceProvenance: "authorized-workspace-operator",
            EvaluatedAtUtc: Now.AddMinutes(-1),
            RequiresDistinctExecutor: true,
            CaseType: DataRightsCaseType.StaffRights,
            ScopeKind: DataRightsExecutionScopeKind.Tenant,
            RetentionDataClass: "staff-employment",
            RetentionTrigger: "employment-ended",
            RetentionTriggeredAtUtc: Now.AddDays(-2_556),
            RetentionDeadlineUtc: Now.AddDays(-1),
            StateBindings:
            [
                new("staff.governance", 1, new string('b', 64)),
                new("staff.holds", 42, new string('c', 64)),
                new("staff.record", 3, new string('d', 64)),
                new("staff.restriction", 0, new string('e', 64))
            ],
            StateBindingsSha256: new string('f', 64));

    private static StaffAnonymisationReceiptDto CreateReceipt(
        DataRightsAnonymisationContributionRequestV2 request) =>
        new(
            ContractVersion: 1,
            Guid.NewGuid(),
            request.IdempotencyKey,
            request.CaseId,
            request.ApprovalRevision,
            request.OperationRevision,
            request.Coordinate.RecordId,
            request.Coordinate.RecordVersion,
            ResultingStaffVersion:
                request.Coordinate.RecordVersion + 1,
            SelectedOperationLockRevision: 42,
            ResultingOperationLockRevision: 43,
            StaffAnonymisationDisposition.Completed,
            StaffAnonymisationReason.ProfileAnonymised,
            new string('a', 64),
            request.ApprovalEvidence.StateBindingsSha256!,
            Guid.NewGuid(),
            request.ExecutingActorId,
            Now.AddSeconds(30),
            new string('b', 64));

    private static DataRightsAnonymisationRestoreRequestV3
        CreateRestoreRequest() =>
        new(
            DataRightsAnonymisationRestoreContractV3.CurrentVersion,
            "tenant-a",
            Guid.NewGuid(),
            TenantSequence: 3,
            new string('a', 64),
            DataRightsCaseType.StaffRights,
            DataRightsExecutionScopeKind.Tenant,
            RoutingPropertyId: null,
            StaffDataRightsCoordinates.Owner,
            StaffDataRightsCoordinates.StaffMemberRecordType,
            Guid.NewGuid(),
            OwnerReceiptContractVersion: 1,
            Guid.NewGuid(),
            new string('b', 64),
            ResultingRecordVersion: 4,
            Now.AddDays(-1));

    private sealed class FakeDispatcher(
        Result<StaffAnonymisationReceiptDto> result)
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

    private sealed class FakeRestoreDispatcher(
        Result<StaffAnonymisationRestoreReceipt> result)
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
}
