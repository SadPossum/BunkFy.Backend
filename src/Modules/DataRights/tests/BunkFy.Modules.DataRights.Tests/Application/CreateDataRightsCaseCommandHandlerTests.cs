namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
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
        CreateDataRightsCaseCommandHandler handler = new(
            repository,
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
    }

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
}
