namespace BunkFy.Modules.DataRights.Tests.Application;

using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Application.Queries;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Xunit;

[Trait("Category", "Unit")]
public sealed class GetDataRightsExportArtifactQueryHandlerTests
{
    private static readonly DateTimeOffset RequestedAt =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Available_artifact_is_reported_expired_after_frozen_expiry()
    {
        DataRightsExportArtifact artifact = Artifact();
        Guid runId = Guid.NewGuid();
        _ = artifact.BeginGeneration(
            runId,
            attempt: 1,
            "system:data-rights-export",
            RequestedAt.AddMinutes(1));
        _ = artifact.MarkAvailable(
            runId,
            attempt: 1,
            "data-rights/exports/artifact.bfdrx",
            encryptedByteLength: 100,
            new string('a', 64),
            encryptionKeyVersion: 1,
            formatVersion: 1,
            RequestedAt.AddMinutes(2),
            RequestedAt.AddHours(24));
        GetDataRightsExportArtifactQueryHandler handler = new(
            new StubRepository(artifact),
            new TestClock(RequestedAt.AddHours(24)));

        Result<DataRightsExportArtifactDto> result = await handler.HandleAsync(
            new GetDataRightsExportArtifactQuery(
                DataRightsCaseScope.Staff,
                artifact.CaseId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DataRightsExportArtifactStatus.Expired, result.Value.Status);
        Assert.Equal(RequestedAt.AddHours(24), result.Value.ExpiresAtUtc);
    }

    [Fact]
    public async Task Pending_artifact_is_reported_expired_after_frozen_expiry()
    {
        DataRightsExportArtifact artifact = Artifact();
        GetDataRightsExportArtifactQueryHandler handler = new(
            new StubRepository(artifact),
            new TestClock(RequestedAt.AddHours(24)));

        Result<DataRightsExportArtifactDto> result = await handler.HandleAsync(
            new GetDataRightsExportArtifactQuery(
                DataRightsCaseScope.Staff,
                artifact.CaseId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DataRightsExportArtifactStatus.Expired, result.Value.Status);
    }

    [Fact]
    public async Task Active_deletion_status_is_not_hidden_by_effective_expiry()
    {
        DataRightsExportArtifact artifact = Artifact();
        _ = artifact.MarkExpired(RequestedAt.AddHours(24));
        _ = artifact.BeginDeletion(
            Guid.NewGuid(),
            RequestedAt.AddHours(24));
        GetDataRightsExportArtifactQueryHandler handler = new(
            new StubRepository(artifact),
            new TestClock(RequestedAt.AddHours(25)));

        Result<DataRightsExportArtifactDto> result = await handler.HandleAsync(
            new GetDataRightsExportArtifactQuery(
                DataRightsCaseScope.Staff,
                artifact.CaseId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DataRightsExportArtifactStatus.Deleting, result.Value.Status);
    }

    [Fact]
    public async Task Missing_artifact_is_not_found()
    {
        GetDataRightsExportArtifactQueryHandler handler = new(
            new StubRepository(null),
            new TestClock(RequestedAt));

        Result<DataRightsExportArtifactDto> result = await handler.HandleAsync(
            new GetDataRightsExportArtifactQuery(
                DataRightsCaseScope.Staff,
                Guid.NewGuid()),
            CancellationToken.None);

        Assert.Equal(
            DataRightsApplicationErrors.ExportArtifactNotFound.Code,
            result.Error.Code);
    }

    private static DataRightsExportArtifact Artifact() =>
        DataRightsExportArtifact.Request(
            Guid.NewGuid(),
            "tenant-a",
            Guid.NewGuid(),
            Guid.NewGuid(),
            propertyId: null,
            DataRightsCaseKind.StaffRights,
            decisionRevision: 7,
            selectedSubjectCount: 1,
            new string('a', 64),
            "user:privacy",
            RequestedAt,
            RequestedAt.AddHours(24)).Value;

    private sealed class StubRepository(DataRightsExportArtifact? artifact)
        : IDataRightsExportArtifactRepository
    {
        public Task AddAsync(
            DataRightsExportArtifact value,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DataRightsExportArtifact?> GetAsync(
            DataRightsCaseScope scope,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                artifact?.Id == artifactId ? artifact : null);

        public Task<DataRightsExportArtifact?> GetByCaseAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                artifact?.CaseId == caseId ? artifact : null);

        public Task<DataRightsExportArtifact?> GetByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<DataRightsExportArtifact?>(null);
    }

    private sealed class TestClock(DateTimeOffset utcNow) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
