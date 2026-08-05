namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class CreateDataRightsCaseCommandHandlerTests
{
    private static readonly Guid CaseId =
        Guid.Parse("8d000000-0000-0000-0000-000000000010");
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Staff_rights_case_is_persisted_in_tenant_scope()
    {
        RecordingRepository repository = new();
        StubDeadlinePolicy deadlinePolicy = new();
        CreateDataRightsCaseCommandHandler handler = new(
            repository,
            deadlinePolicy,
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            new CreateDataRightsCaseCommand(
                DataRightsCaseScope.Staff,
                DataRightsOperation.AccessExport,
                DataRightsRestrictionDirective.Unknown,
                DataRightsRequesterRelationship.DataSubject,
                "user:privacy"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(CaseId, result.Value.Id);
        Assert.Equal(DataRightsCaseType.StaffRights, result.Value.Type);
        Assert.Null(result.Value.PropertyId);
        Assert.NotNull(repository.Added);
        Assert.Equal("tenant-a", repository.Added.ScopeId);
        Assert.Null(repository.Added.PropertyId);
        Assert.Equal(0, deadlinePolicy.EvaluationCount);
    }

    [Fact]
    public async Task Guest_rights_intake_is_persisted_when_deadline_policy_is_unavailable()
    {
        RecordingRepository repository = new();
        StubDeadlinePolicy deadlinePolicy = new();
        CreateDataRightsCaseCommandHandler handler = new(
            repository,
            deadlinePolicy,
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            new CreateDataRightsCaseCommand(
                DataRightsCaseScope.ForProperty(Guid.NewGuid()),
                DataRightsOperation.AccessExport,
                DataRightsRestrictionDirective.Unknown,
                DataRightsRequesterRelationship.DataSubject,
                "user:privacy"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.Added);
        Assert.Null(result.Value.DueAtUtc);
        Assert.Null(result.Value.ResponseDeadlineEvidence);
        Assert.Equal(1, deadlinePolicy.EvaluationCount);
    }

    [Fact]
    public async Task Guest_rights_intake_freezes_an_available_deadline()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsResponseDeadlinePolicyEvidence evidence = CreateEvidence(propertyId);
        RecordingRepository repository = new();
        StubDeadlinePolicy deadlinePolicy = new(Result.Success(evidence));
        CreateDataRightsCaseCommandHandler handler = new(
            repository,
            deadlinePolicy,
            new TestScopeContext(),
            new TestClock(),
            new TestIdGenerator());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            new CreateDataRightsCaseCommand(
                DataRightsCaseScope.ForProperty(propertyId),
                DataRightsOperation.AccessExport,
                DataRightsRestrictionDirective.Unknown,
                DataRightsRequesterRelationship.DataSubject,
                "user:privacy"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(evidence.DueAtUtc, result.Value.DueAtUtc);
        Assert.Equal(2, result.Value.ResponseDeadlineEvidence!.PolicyVersion);
        Assert.Same(evidence, repository.Added!.ResponseDeadlinePolicyEvidence);
    }

    private static DataRightsResponseDeadlinePolicyEvidence CreateEvidence(
        Guid propertyId) => DataRightsResponseDeadlinePolicyEvidence.Create(
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
            Now,
            Now,
            Now.AddMonths(1)).Value;

    private sealed class RecordingRepository : IDataRightsCaseRepository
    {
        public DataRightsCase? Added { get; private set; }

        public Task AddAsync(
            DataRightsCase dataRightsCase,
            CancellationToken cancellationToken)
        {
            this.Added = dataRightsCase;
            return Task.CompletedTask;
        }

        public Task<DataRightsCase?> GetAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DataRightsCaseListResponse> ListAsync(
            DataRightsCaseScope scope,
            DataRightsCaseStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class TestIdGenerator : IIdGenerator
    {
        public Guid NewId() => CaseId;
    }

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
}
