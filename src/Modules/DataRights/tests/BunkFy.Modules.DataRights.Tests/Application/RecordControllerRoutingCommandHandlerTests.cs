namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class RecordControllerRoutingCommandHandlerTests
{
    private static readonly DateTimeOffset ReceivedAt =
        new(2026, 7, 27, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset RoutedAt = ReceivedAt.AddMinutes(5);

    [Fact]
    public async Task Routing_blocks_without_an_authoritative_deadline()
    {
        DataRightsCase dataRightsCase = CreateCase();
        StubDeadlinePolicy policy = new();
        RecordControllerRoutingCommandHandler handler = new(
            new StubRepository(dataRightsCase),
            policy,
            new TestClock());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            Command(dataRightsCase),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(
            DataRightsApplicationErrors.ResponseDeadlinePolicyUnavailable,
            result.Error);
        Assert.Equal(DataRightsRoutingState.Pending, dataRightsCase.RoutingStatus);
        Assert.Equal(1, dataRightsCase.Version);
        Assert.Equal(1, policy.EvaluationCount);
    }

    [Fact]
    public async Task Routing_atomically_freezes_resolved_deadline_evidence()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateCase(propertyId);
        DataRightsResponseDeadlinePolicyEvidence evidence =
            CreateEvidence(propertyId, RoutedAt);
        StubDeadlinePolicy policy = new(Result.Success(evidence));
        RecordControllerRoutingCommandHandler handler = new(
            new StubRepository(dataRightsCase),
            policy,
            new TestClock());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            Command(dataRightsCase),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DataRightsRoutingStatus.Routed, result.Value.RoutingStatus);
        Assert.Equal(evidence.DueAtUtc, result.Value.DueAtUtc);
        Assert.Equal(evidence.PolicyId, result.Value.ResponseDeadlineEvidence!.PolicyId);
        Assert.Equal(2, dataRightsCase.Version);
    }

    [Fact]
    public async Task Routing_reuses_evidence_frozen_during_intake_without_reevaluation()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsResponseDeadlinePolicyEvidence evidence =
            CreateEvidence(propertyId, ReceivedAt);
        DataRightsCase dataRightsCase = CreateCase(propertyId, evidence);
        StubDeadlinePolicy policy = new();
        RecordControllerRoutingCommandHandler handler = new(
            new StubRepository(dataRightsCase),
            policy,
            new TestClock());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            Command(dataRightsCase),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, policy.EvaluationCount);
        Assert.Equal(evidence.DueAtUtc, result.Value.DueAtUtc);
    }

    private static RecordControllerRoutingCommand Command(
        DataRightsCase dataRightsCase) => new(
        DataRightsCaseScope.ForProperty(dataRightsCase.PropertyId!.Value),
        dataRightsCase.Id,
        dataRightsCase.Version,
        "user:privacy");

    private static DataRightsCase CreateCase(
        Guid? propertyId = null,
        DataRightsResponseDeadlinePolicyEvidence? evidence = null)
    {
        Guid resolvedPropertyId = propertyId ?? Guid.NewGuid();
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            resolvedPropertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.DataSubject).Value;
        return DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:intake",
            ReceivedAt,
            evidence).Value;
    }

    private static DataRightsResponseDeadlinePolicyEvidence CreateEvidence(
        Guid propertyId,
        DateTimeOffset evaluatedAtUtc) =>
        DataRightsResponseDeadlinePolicyEvidence.Create(
            propertyId,
            7,
            8,
            "GB",
            "development-hostel-example",
            2,
            new string('a', 64),
            DataRightsResponseRight.Export,
            "development-example",
            0,
            1,
            0,
            "Europe/London",
            new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ReceivedAt,
            evaluatedAtUtc,
            ReceivedAt.AddMonths(1)).Value;

    private sealed class StubDeadlinePolicy(
        Result<DataRightsResponseDeadlinePolicyEvidence>? result = null)
        : IDataRightsResponseDeadlinePolicy
    {
        private readonly Result<DataRightsResponseDeadlinePolicyEvidence> result =
            result ?? Result.Failure<DataRightsResponseDeadlinePolicyEvidence>(
                DataRightsApplicationErrors.ResponseDeadlinePolicyUnavailable);

        public int EvaluationCount { get; private set; }

        public Task<Result<DataRightsResponseDeadlinePolicyEvidence>> ResolveGuestAsync(
            Guid propertyId,
            DataRightsCaseOperation requestedOperations,
            DateTimeOffset receivedAtUtc,
            DateTimeOffset evaluatedAtUtc,
            CancellationToken cancellationToken)
        {
            this.EvaluationCount++;
            return Task.FromResult(this.result);
        }
    }

    private sealed class StubRepository(DataRightsCase dataRightsCase)
        : IDataRightsCaseRepository
    {
        public Task AddAsync(
            DataRightsCase added,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DataRightsCase?> GetAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult<DataRightsCase?>(
                caseId == dataRightsCase.Id ? dataRightsCase : null);

        public Task<DataRightsCaseListResponse> ListAsync(
            DataRightsCaseScope scope,
            DataRightsCaseStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => RoutedAt;
    }
}
