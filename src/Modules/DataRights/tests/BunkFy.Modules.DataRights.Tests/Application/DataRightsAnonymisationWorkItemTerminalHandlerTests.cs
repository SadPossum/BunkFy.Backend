namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Entities;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Pagination;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsAnonymisationWorkItemTerminalHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 26, 22, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Reconciliation_waits_for_all_owners_then_records_partial_completion()
    {
        ExecutionFixture fixture = CreateFixture();
        MutableClock clock = new(Now.AddMinutes(5));
        DataRightsAnonymisationWorkItemTerminalHandler handler = new(
            new StubCaseRepository(fixture.Case),
            new StubBatchRepository(fixture.Batch),
            new StubWorkItemRepository(fixture.WorkItems),
            clock);

        await handler.HandleAsync(
            TerminalEvent(fixture, fixture.WorkItems[0], clock.UtcNow),
            CancellationToken.None);
        Assert.Equal(DataRightsCaseState.Executing, fixture.Case.Status);

        DataRightsExecutionWorkItem blocked = fixture.WorkItems[1];
        Assert.True(blocked.RecordBlocked(
            blocked.Version,
            blocked.TaskRunId!.Value,
            blocked.LastTaskAttempt,
            "reservations.retention-blocked",
            Now.AddMinutes(6)).IsSuccess);
        clock.UtcNow = Now.AddMinutes(7);
        await handler.HandleAsync(
            TerminalEvent(fixture, blocked, clock.UtcNow),
            CancellationToken.None);

        Assert.Equal(DataRightsCaseState.PartiallyCompleted, fixture.Case.Status);
        Assert.Equal(9, fixture.Case.Version);
    }

    private static DataRightsAnonymisationWorkItemTerminalIntegrationEvent TerminalEvent(
        ExecutionFixture fixture,
        DataRightsExecutionWorkItem workItem,
        DateTimeOffset occurredAtUtc) =>
        new(
            Guid.NewGuid(),
            fixture.Case.ScopeId,
            occurredAtUtc,
            fixture.Batch.Id,
            workItem.Id,
            fixture.Case.Id,
            fixture.Batch.PropertyId,
            fixture.Batch.ExecutionRevision);

    private static ExecutionFixture CreateFixture()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.Anonymisation,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:operator",
            Now.AddMinutes(-8)).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            1,
            "user:operator",
            Now.AddMinutes(-7)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            3,
            2,
            "user:operator",
            Now.AddMinutes(-6)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "reservations",
            "reservation",
            Guid.NewGuid(),
            4,
            3,
            "user:operator",
            Now.AddMinutes(-5)).IsSuccess);
        Assert.True(dataRightsCase.RequireReview(
            4,
            "user:operator",
            Now.AddMinutes(-4)).IsSuccess);
        Assert.True(dataRightsCase.BeginDecision(
            5,
            "user:approver",
            Now.AddMinutes(-3)).IsSuccess);
        DataRightsApprovalPolicyEvidence evidence =
            DataRightsApprovalPolicyEvidence.Create(
                propertyId,
                11,
                "GB",
                "approved-policy",
                3,
                "guest-retention",
                2,
                new string('a', 64),
                "data-rights-anonymisation",
                "erasure",
                "authorized-workspace-operator",
                Now.AddMinutes(-2)).Value;
        Assert.True(dataRightsCase.RecordDecision(
            DataRightsCaseDecision.Approved,
            DataRightsCaseDecisionReason.RequestValidated,
            6,
            "user:approver",
            Now.AddMinutes(-2),
            evidence).IsSuccess);
        Assert.True(dataRightsCase.BeginAnonymisationExecution(
            7,
            "user:executor",
            Now.AddMinutes(-1)).IsSuccess);

        DataRightsExecutionBatch batch = DataRightsExecutionBatch.Prepare(
            Guid.NewGuid(),
            dataRightsCase.ScopeId,
            Guid.NewGuid(),
            dataRightsCase.Id,
            propertyId,
            approvalRevision: 7,
            executionRevision: 8,
            selectedSubjectCount: 2,
            "user:executor",
            Now.AddMinutes(-1)).Value;
        DataRightsExecutionWorkItem[] workItems = dataRightsCase.SelectedSubjects
            .OrderBy(subject => subject.OwnerKey, StringComparer.Ordinal)
            .Select(subject => DataRightsExecutionWorkItem.Prepare(
                Guid.NewGuid(),
                dataRightsCase.ScopeId,
                batch.Id,
                Guid.NewGuid(),
                dataRightsCase.Id,
                propertyId,
                approvalRevision: 7,
                executionRevision: 8,
                DataRightsCaseOperation.Anonymisation,
                subject,
                evidence,
                "user:executor",
                Now.AddMinutes(-1)).Value)
            .ToArray();
        foreach (DataRightsExecutionWorkItem workItem in workItems)
        {
            Assert.True(workItem.BeginProcessing(
                Guid.NewGuid(),
                taskAttempt: 1,
                Now.AddMinutes(1)).IsSuccess);
        }

        DataRightsExecutionWorkItem completed = workItems[0];
        Assert.True(completed.RecordOwnerProof(
            completed.Version,
            completed.TaskRunId!.Value,
            completed.LastTaskAttempt,
            receiptContractVersion: 1,
            Guid.NewGuid(),
            completed.SelectedRecordVersion + 1,
            "guests.completed",
            "guests.profile-anonymised",
            new string('b', 64),
            Now.AddMinutes(2),
            Now.AddMinutes(3)).IsSuccess);
        Assert.True(completed.CompleteAfterDurableLedger(
            completed.Version,
            Now.AddMinutes(4)).IsSuccess);
        return new(dataRightsCase, batch, workItems);
    }

    private sealed record ExecutionFixture(
        DataRightsCase Case,
        DataRightsExecutionBatch Batch,
        DataRightsExecutionWorkItem[] WorkItems);

    private sealed class StubCaseRepository(DataRightsCase dataRightsCase)
        : IDataRightsCaseRepository
    {
        public Task AddAsync(
            DataRightsCase ignored,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DataRightsCase?> GetAsync(
            Guid propertyId,
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                dataRightsCase.PropertyId == propertyId && dataRightsCase.Id == caseId
                    ? dataRightsCase
                    : null);

        public Task<DataRightsCaseListResponse> ListAsync(
            Guid propertyId,
            DataRightsCaseStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubBatchRepository(DataRightsExecutionBatch batch)
        : IDataRightsExecutionBatchRepository
    {
        public Task AddAsync(
            DataRightsExecutionBatch ignored,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DataRightsExecutionBatch?> GetByCaseAsync(
            Guid propertyId,
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                batch.PropertyId == propertyId && batch.CaseId == caseId
                    ? batch
                    : null);
    }

    private sealed class StubWorkItemRepository(
        IReadOnlyCollection<DataRightsExecutionWorkItem> workItems)
        : IDataRightsExecutionWorkItemRepository
    {
        public Task AddAsync(
            DataRightsExecutionWorkItem ignored,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<DataRightsExecutionWorkItem>> ListByBatchAsync(
            Guid propertyId,
            Guid caseId,
            Guid batchId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                (IReadOnlyCollection<DataRightsExecutionWorkItem>)workItems
                    .Where(item =>
                        item.PropertyId == propertyId &&
                        item.CaseId == caseId &&
                        item.BatchId == batchId)
                    .ToArray());

        public Task<DataRightsExecutionWorkItem?> GetAsync(
            Guid propertyId,
            Guid caseId,
            Guid workItemId,
            CancellationToken cancellationToken) =>
            Task.FromResult(workItems.SingleOrDefault(item =>
                item.PropertyId == propertyId &&
                item.CaseId == caseId &&
                item.Id == workItemId));
    }

    private sealed class MutableClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }
}
