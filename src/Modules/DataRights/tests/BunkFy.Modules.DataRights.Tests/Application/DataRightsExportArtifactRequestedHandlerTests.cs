namespace BunkFy.Modules.DataRights.Tests.Application;

using System.Text.Json;
using BunkFy.Modules.DataRights.Application.Tasks;
using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Tasks;
using Xunit;

[Trait("Category", "Unit")]
public sealed class DataRightsExportArtifactRequestedHandlerTests
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Request_event_enqueues_generation_and_frozen_expiry_cleanup()
    {
        RecordingTaskRunStore taskRuns = new();
        Guid cleanupRunId = Guid.NewGuid();
        DataRightsExportArtifactRequestedHandler handler = new(
            taskRuns,
            new FixedIdGenerator(cleanupRunId));
        DateTimeOffset occurredAtUtc =
            new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset expiresAtUtc = occurredAtUtc.AddHours(24);
        Guid artifactId = Guid.NewGuid();
        Guid caseId = Guid.NewGuid();

        await handler.HandleAsync(
            new DataRightsExportArtifactRequestedIntegrationEvent(
                Guid.NewGuid(),
                "tenant-a",
                occurredAtUtc,
                artifactId,
                caseId,
                DataRightsCaseType.StaffRights,
                propertyId: null,
                decisionRevision: 7,
                expiresAtUtc),
            CancellationToken.None);

        Assert.Equal(2, taskRuns.Requests.Count);
        TaskRunRequest generation = Assert.Single(
            taskRuns.Requests,
            request =>
                request.TaskName ==
                GenerateDataRightsExportPayload.TaskName);
        Assert.Equal(occurredAtUtc, generation.ScheduledAtUtc);
        Assert.DoesNotContain("subject", generation.PayloadJson);

        TaskRunRequest cleanup = Assert.Single(
            taskRuns.Requests,
            request =>
                request.TaskName ==
                DeleteExpiredDataRightsExportArtifactPayload.TaskName);
        Assert.Equal(cleanupRunId, cleanup.RunId);
        Assert.Equal(expiresAtUtc, cleanup.ScheduledAtUtc);
        Assert.Equal("tenant-a", cleanup.ScopeId);
        DeleteExpiredDataRightsExportArtifactPayload payload =
            JsonSerializer.Deserialize<
                DeleteExpiredDataRightsExportArtifactPayload>(
                cleanup.PayloadJson,
                SerializerOptions)!;
        Assert.Equal(artifactId, payload.ArtifactId);
        Assert.Equal(caseId, payload.CaseId);
        Assert.Equal(expiresAtUtc, payload.ExpiresAtUtc);
    }

    private sealed class RecordingTaskRunStore : ITaskRunStore
    {
        public List<TaskRunRequest> Requests { get; } = [];

        public Task<TaskRunEnqueueResult> EnqueueAsync(
            TaskRunRequest request,
            CancellationToken cancellationToken)
        {
            this.Requests.Add(request);
            return Task.FromResult<TaskRunEnqueueResult>(null!);
        }

        public Task<TaskRunPage> ListAsync(
            TaskRunFilter filter,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunDetails?> GetAsync(
            Guid runId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunStats> GetStatsAsync(
            TaskRunStatsFilter filter,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TaskRunLease>> ClaimReadyAsync(
            TaskWorkerClaim claim,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkStartedAsync(
            TaskExecutionContext context,
            DateTimeOffset startedAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkSucceededAsync(
            TaskExecutionContext context,
            DateTimeOffset completedAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkCanceledAsync(
            TaskExecutionContext context,
            DateTimeOffset canceledAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkFailedAsync(
            TaskExecutionContext context,
            string error,
            DateTimeOffset failedAtUtc,
            DateTimeOffset? retryAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> RequestCancellationAsync(
            Guid runId,
            string? requestedBy,
            DateTimeOffset requestedAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> RetryAsync(
            Guid runId,
            string? requestedBy,
            DateTimeOffset scheduledAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TaskRunSummary>> MarkStaleTimedOutAsync(
            DateTimeOffset nowUtc,
            TimeSpan staleAfter,
            int maxRuns,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskControlMessageEnqueueOutcome> EnqueueControlMessageAsync(
            TaskControlMessage message,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> ReportHeartbeatAsync(
            TaskExecutionContext context,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> ReportProgressAsync(
            TaskExecutionContext context,
            TaskProgress progress,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TaskControlMessage>> ReadPendingAsync(
            TaskExecutionContext context,
            int maxMessages,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkHandledAsync(
            TaskExecutionContext context,
            Guid messageId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TaskRunMutationOutcome> MarkFailedAsync(
            TaskExecutionContext context,
            Guid messageId,
            string error,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FixedIdGenerator(Guid id) : IIdGenerator
    {
        public Guid NewId() => id;
    }
}
