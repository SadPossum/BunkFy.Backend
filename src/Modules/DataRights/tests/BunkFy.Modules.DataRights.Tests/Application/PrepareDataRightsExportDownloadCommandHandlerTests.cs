namespace BunkFy.Modules.DataRights.Tests.Application;

using System.Text;
using BunkFy.Modules.DataRights.Application;
using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Handlers;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Application.Ports;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;
using Xunit;

[Trait("Category", "Unit")]
public sealed class PrepareDataRightsExportDownloadCommandHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Available_verified_artifact_is_audited_before_delivery()
    {
        DataRightsExportArtifact artifact = AvailableArtifact();
        RecordingAuditSink audit = new();
        PrepareDataRightsExportDownloadCommandHandler handler = new(
            new ArtifactRepository(artifact),
            new Reader(),
            audit,
            new ScopeContext(),
            new Clock(Now.AddMinutes(3)));

        Result<DataRightsExportDownload> result = await handler.HandleAsync(
            new PrepareDataRightsExportDownloadCommand(
                DataRightsCaseScope.Staff,
                artifact.CaseId,
                artifact.Id,
                "user:privacy"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        DataRightsExportAuditFact fact = Assert.Single(audit.Facts);
        Assert.Equal(DataRightsExportAuditAction.Download, fact.Action);
        Assert.Equal("succeeded", fact.OutcomeCode);
        Assert.Equal("user:privacy", fact.ActorId);
        Assert.Equal("application-export.json", result.Value.FileName);
        await result.Value.Content.DisposeAsync();
    }

    [Fact]
    public async Task Expired_artifact_fails_closed_and_records_denial()
    {
        DataRightsExportArtifact artifact = AvailableArtifact();
        RecordingAuditSink audit = new();
        Reader reader = new();
        PrepareDataRightsExportDownloadCommandHandler handler = new(
            new ArtifactRepository(artifact),
            reader,
            audit,
            new ScopeContext(),
            new Clock(artifact.ExpiresAtUtc));

        Result<DataRightsExportDownload> result = await handler.HandleAsync(
            new PrepareDataRightsExportDownloadCommand(
                DataRightsCaseScope.Staff,
                artifact.CaseId,
                artifact.Id,
                "user:privacy"),
            CancellationToken.None);

        Assert.Equal(
            DataRightsApplicationErrors.ExportArtifactExpired.Code,
            result.Error.Code);
        Assert.False(reader.WasOpened);
        Assert.Equal(
            DataRightsApplicationErrors.ExportArtifactExpired.Code,
            Assert.Single(audit.Facts).OutcomeCode);
    }

    [Fact]
    public async Task Integrity_failure_is_generic_and_audited()
    {
        DataRightsExportArtifact artifact = AvailableArtifact();
        RecordingAuditSink audit = new();
        PrepareDataRightsExportDownloadCommandHandler handler = new(
            new ArtifactRepository(artifact),
            new Reader(throwOnOpen: true),
            audit,
            new ScopeContext(),
            new Clock(Now.AddMinutes(3)));

        Result<DataRightsExportDownload> result = await handler.HandleAsync(
            new PrepareDataRightsExportDownloadCommand(
                DataRightsCaseScope.Staff,
                artifact.CaseId,
                artifact.Id,
                "user:privacy"),
            CancellationToken.None);

        Assert.Equal(
            DataRightsApplicationErrors.ExportArtifactVerificationFailed.Code,
            result.Error.Code);
        Assert.Equal(
            DataRightsApplicationErrors.ExportArtifactVerificationFailed.Code,
            Assert.Single(audit.Facts).OutcomeCode);
    }

    [Fact]
    public async Task Artifact_expiring_during_verification_is_not_released()
    {
        DataRightsExportArtifact artifact = AvailableArtifact();
        RecordingAuditSink audit = new();
        Reader reader = new();
        PrepareDataRightsExportDownloadCommandHandler handler = new(
            new ArtifactRepository(artifact),
            reader,
            audit,
            new ScopeContext(),
            new AdvancingClock(
                Now.AddMinutes(3),
                artifact.ExpiresAtUtc));

        Result<DataRightsExportDownload> result = await handler.HandleAsync(
            new PrepareDataRightsExportDownloadCommand(
                DataRightsCaseScope.Staff,
                artifact.CaseId,
                artifact.Id,
                "user:privacy"),
            CancellationToken.None);

        Assert.Equal(
            DataRightsApplicationErrors.ExportArtifactExpired.Code,
            result.Error.Code);
        Assert.NotNull(reader.Content);
        Assert.False(reader.Content.CanRead);
        Assert.Equal(
            DataRightsApplicationErrors.ExportArtifactExpired.Code,
            Assert.Single(audit.Facts).OutcomeCode);
    }

    private static DataRightsExportArtifact AvailableArtifact()
    {
        DataRightsExportArtifact artifact = DataRightsExportArtifact.Request(
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
            Now,
            Now.AddHours(24)).Value;
        Guid runId = Guid.NewGuid();
        _ = artifact.BeginGeneration(
            runId,
            attempt: 1,
            "system:data-rights-export",
            Now.AddMinutes(1));
        _ = artifact.MarkAvailable(
            runId,
            attempt: 1,
            "data-rights/exports/scope-test/artifact-test.bfdrx",
            encryptedByteLength: 100,
            new string('b', 64),
            encryptionKeyVersion: 1,
            formatVersion: 1,
            Now.AddMinutes(2),
            Now.AddHours(24));
        return artifact;
    }

    private sealed class ArtifactRepository(DataRightsExportArtifact artifact)
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
            Task.FromResult<DataRightsExportArtifact?>(
                artifact.Id == artifactId ? artifact : null);

        public Task<DataRightsExportArtifact?> GetByCaseAsync(
            DataRightsCaseScope scope,
            Guid caseId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<DataRightsExportArtifact?> GetByIdempotencyKeyAsync(
            Guid idempotencyKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class Reader(bool throwOnOpen = false)
        : IDataRightsExportArtifactReader
    {
        public bool WasOpened { get; private set; }
        public Stream? Content { get; private set; }

        public Task<DataRightsExportDownload> OpenVerifiedAsync(
            DataRightsExportArtifact artifact,
            CancellationToken cancellationToken)
        {
            this.WasOpened = true;
            if (throwOnOpen)
            {
                throw new InvalidOperationException("raw-storage-detail");
            }

            byte[] bytes = Encoding.UTF8.GetBytes("{}");
            this.Content = new MemoryStream(bytes, writable: false);
            return Task.FromResult(new DataRightsExportDownload(
                this.Content,
                bytes.Length,
                "application-export.json"));
        }
    }

    private sealed class RecordingAuditSink : IDataRightsExportAuditSink
    {
        public List<DataRightsExportAuditFact> Facts { get; } = [];

        public Task RecordAsync(
            DataRightsExportAuditFact fact,
            CancellationToken cancellationToken)
        {
            this.Facts.Add(fact);
            return Task.CompletedTask;
        }
    }

    private sealed class ScopeContext : IScopeContext
    {
        public bool IsEnabled => true;
        public string ScopeId => "tenant-a";
    }

    private sealed class Clock(DateTimeOffset nowUtc) : ISystemClock
    {
        public DateTimeOffset UtcNow => nowUtc;
    }

    private sealed class AdvancingClock(
        DateTimeOffset initialUtc,
        DateTimeOffset verifiedUtc) : ISystemClock
    {
        private int reads;

        public DateTimeOffset UtcNow =>
            Interlocked.Increment(ref this.reads) == 1
                ? initialUtc
                : verifiedUtc;
    }
}
