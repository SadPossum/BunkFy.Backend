namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using BunkFy.Modules.DataRights.Domain.ValueObjects;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsSubjectDiscoveryHandlerTests
{
    [Fact]
    public async Task Duplicate_candidates_do_not_starve_later_owners()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDiscoveryCase(propertyId);
        DataRightsSubjectCandidate first = Candidate("alpha", Guid.NewGuid());
        DataRightsSubjectCandidate second = Candidate("beta", Guid.NewGuid());
        StubContributor alpha = new(
            "alpha",
            request => DataRightsSubjectDiscoveryResult.Success(
                Enumerable.Repeat(first, request.MaxCandidates).ToArray()));
        StubContributor beta = new(
            "beta",
            _ => DataRightsSubjectDiscoveryResult.Success([second]));
        DiscoverDataRightsSubjectsQueryHandler handler = new(
            new CaseRepository(dataRightsCase),
            [beta, alpha],
            new TestScopeContext());

        Result<DataRightsSubjectDiscoveryResponse> result = await handler.HandleAsync(
            new DiscoverDataRightsSubjectsQuery(
                propertyId,
                dataRightsCase.Id,
                new DataRightsSubjectLookup(null, "guest@example.test", null, null, null)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([first, second], result.Value.Candidates);
    }

    [Fact]
    public async Task Malformed_contributor_candidate_fails_closed_without_throwing()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDiscoveryCase(propertyId);
        DataRightsSubjectCandidate malformed = new(
            new DataRightsSubjectCoordinate(null!, "guest-profile", Guid.NewGuid(), 1),
            "Guest",
            null,
            null);
        DiscoverDataRightsSubjectsQueryHandler handler = new(
            new CaseRepository(dataRightsCase),
            [
                new StubContributor(
                    "guests",
                    _ => DataRightsSubjectDiscoveryResult.Success([malformed]))
            ],
            new TestScopeContext());

        Result<DataRightsSubjectDiscoveryResponse> result = await handler.HandleAsync(
            new DiscoverDataRightsSubjectsQuery(
                propertyId,
                dataRightsCase.Id,
                new DataRightsSubjectLookup(null, null, "+44 20 1234 5678", null, null)),
            CancellationToken.None);

        Assert.Equal(DataRightsApplicationErrors.SubjectCoordinateInvalid, result.Error);
    }

    [Fact]
    public async Task Null_discovery_result_fails_closed_without_throwing()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDiscoveryCase(propertyId);
        DiscoverDataRightsSubjectsQueryHandler handler = new(
            new CaseRepository(dataRightsCase),
            [new StubContributor("guests", _ => null!)],
            new TestScopeContext());

        Result<DataRightsSubjectDiscoveryResponse> result = await handler.HandleAsync(
            new DiscoverDataRightsSubjectsQuery(
                propertyId,
                dataRightsCase.Id,
                new DataRightsSubjectLookup(null, "guest@example.test", null, null, null)),
            CancellationToken.None);

        Assert.Equal(DataRightsApplicationErrors.DiscoveryScopeUnavailable, result.Error);
    }

    [Fact]
    public async Task Owner_cannot_validate_a_different_selection()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDiscoveryCase(propertyId);
        DataRightsSubjectCoordinate requested = new(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            4);
        StubContributor contributor = new(
            "guests",
            _ => DataRightsSubjectDiscoveryResult.Success([]),
            _ => DataRightsSubjectSelectionValidation.Valid(
                requested with { RecordId = Guid.NewGuid() }));
        SelectDataRightsSubjectCommandHandler handler = new(
            new CaseRepository(dataRightsCase),
            [contributor],
            new TestScopeContext(),
            new TestClock());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            new SelectDataRightsSubjectCommand(
                propertyId,
                dataRightsCase.Id,
                requested,
                dataRightsCase.Version,
                "user:operator"),
            CancellationToken.None);

        Assert.Equal(DataRightsApplicationErrors.SubjectCoordinateInvalid, result.Error);
        Assert.Empty(dataRightsCase.SelectedSubjects);
    }

    [Fact]
    public async Task Null_selection_validation_fails_closed_without_throwing()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDiscoveryCase(propertyId);
        DataRightsSubjectCoordinate requested = new(
            "guests",
            "guest-profile",
            Guid.NewGuid(),
            4);
        StubContributor contributor = new(
            "guests",
            _ => DataRightsSubjectDiscoveryResult.Success([]),
            _ => null!);
        SelectDataRightsSubjectCommandHandler handler = new(
            new CaseRepository(dataRightsCase),
            [contributor],
            new TestScopeContext(),
            new TestClock());

        Result<DataRightsCaseDto> result = await handler.HandleAsync(
            new SelectDataRightsSubjectCommand(
                propertyId,
                dataRightsCase.Id,
                requested,
                dataRightsCase.Version,
                "user:operator"),
            CancellationToken.None);

        Assert.Equal(DataRightsApplicationErrors.SubjectCoordinateInvalid, result.Error);
        Assert.Empty(dataRightsCase.SelectedSubjects);
    }

    [Fact]
    public async Task Selected_subjects_can_be_resumed_without_exposing_previews()
    {
        Guid propertyId = Guid.NewGuid();
        DataRightsCase dataRightsCase = CreateDiscoveryCase(propertyId);
        Guid laterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid earlierId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            laterId,
            5,
            dataRightsCase.Version,
            "user:operator",
            dataRightsCase.LastChangedAtUtc.AddMinutes(1)).IsSuccess);
        Assert.True(dataRightsCase.SelectSubject(
            "guests",
            "guest-profile",
            earlierId,
            3,
            dataRightsCase.Version,
            "user:operator",
            dataRightsCase.LastChangedAtUtc.AddMinutes(1)).IsSuccess);
        GetDataRightsSelectedSubjectsQueryHandler handler =
            new(new CaseRepository(dataRightsCase));

        Result<DataRightsSelectedSubjectsResponse> result = await handler.HandleAsync(
            new GetDataRightsSelectedSubjectsQuery(propertyId, dataRightsCase.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(dataRightsCase.Version, result.Value.CaseVersion);
        Assert.Equal(
            [earlierId, laterId],
            result.Value.Subjects.Select(subject => subject.RecordId));
        Assert.All(result.Value.Subjects, subject =>
        {
            Assert.Equal("guests", subject.OwnerKey);
            Assert.Equal("guest-profile", subject.RecordType);
        });
    }

    [Fact]
    public void Duplicate_owner_registrations_fail_closed()
    {
        StubContributor first = new(
            "guests",
            _ => DataRightsSubjectDiscoveryResult.Success([]));
        StubContributor duplicate = new(
            " GUESTS ",
            _ => DataRightsSubjectDiscoveryResult.Success([]));

        Result<IReadOnlyCollection<IDataRightsSubjectDiscoveryContributor>> result =
            DataRightsSubjectContributorSet.Order([first, duplicate]);

        Assert.Equal(DataRightsApplicationErrors.SubjectOwnerUnavailable, result.Error);
    }

    private static DataRightsSubjectCandidate Candidate(string owner, Guid recordId) => new(
        new DataRightsSubjectCoordinate(owner, "guest-profile", recordId, 1),
        $"{owner} guest",
        null,
        null);

    private static DataRightsCase CreateDiscoveryCase(Guid propertyId)
    {
        DataRightsCaseRequest request = DataRightsCaseRequest.Create(
            propertyId,
            DataRightsCaseKind.GuestRights,
            DataRightsCaseOperation.AccessExport,
            DataRightsRequesterRelation.ControllerInitiated).Value;
        DataRightsCase dataRightsCase = DataRightsCase.Create(
            Guid.NewGuid(),
            "tenant-a",
            request,
            "user:operator",
            new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero)).Value;
        Assert.True(dataRightsCase.BeginDiscovery(
            dataRightsCase.Version,
            "user:operator",
            dataRightsCase.CreatedAtUtc.AddMinutes(1)).IsSuccess);
        return dataRightsCase;
    }

    private sealed class CaseRepository(DataRightsCase dataRightsCase)
        : IDataRightsCaseRepository
    {
        public Task AddAsync(
            DataRightsCase added,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<DataRightsCase?> GetAsync(
            Guid propertyId,
            Guid caseId,
            CancellationToken cancellationToken) => Task.FromResult(
            dataRightsCase.PropertyId == propertyId && dataRightsCase.Id == caseId
                ? dataRightsCase
                : null);

        public Task<DataRightsCaseListResponse> ListAsync(
            Guid propertyId,
            DataRightsCaseStatus? status,
            PageRequest pageRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubContributor(
        string ownerKey,
        Func<DataRightsSubjectDiscoveryRequest, DataRightsSubjectDiscoveryResult> discover,
        Func<DataRightsSubjectSelectionRequest, DataRightsSubjectSelectionValidation>? validate = null)
        : IDataRightsSubjectDiscoveryContributor
    {
        public string OwnerKey => ownerKey;

        public Task<DataRightsSubjectDiscoveryResult> DiscoverAsync(
            DataRightsSubjectDiscoveryRequest request,
            CancellationToken cancellationToken) => Task.FromResult(discover(request));

        public Task<DataRightsSubjectSelectionValidation> ValidateSelectionAsync(
            DataRightsSubjectSelectionRequest request,
            CancellationToken cancellationToken) => validate is null
            ? Task.FromResult(DataRightsSubjectSelectionValidation.NotFound())
            : Task.FromResult(validate(request));
    }

    private sealed class TestScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTimeOffset UtcNow =>
            new(2026, 7, 23, 12, 2, 0, TimeSpan.Zero);
    }
}
