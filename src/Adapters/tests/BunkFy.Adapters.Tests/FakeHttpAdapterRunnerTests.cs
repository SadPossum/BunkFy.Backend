namespace BunkFy.Adapters.Tests;

using System.Net;
using System.Text;
using BunkFy.Adapter.Abstractions;
using BunkFy.Adapters.FakeHttp;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[Trait("Category", "Unit")]
public sealed class FakeHttpAdapterRunnerTests
{
    [Fact]
    public async Task Polls_from_checkpoint_and_submits_deterministic_observation()
    {
        RecordingHandler handler = new(ResponseJson);
        ServiceCollection services = new();
        services.AddFakeHttpAdapter().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using ServiceProvider provider = services.BuildServiceProvider();
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        RecordingSink sink = new(checkpointAccepted: true);
        AdapterRunAssignment assignment = CreateAssignment("cursor-1");
        using AdapterConfigurationMaterial material = CreateMaterial();

        AdapterRunCompletion first = await runner.RunAsync(assignment, material, sink, CancellationToken.None);
        Guid firstOperationId = Assert.Single(sink.Records).OperationId;
        sink.Records.Clear();
        AdapterRunCompletion second = await runner.RunAsync(assignment, material, sink, CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Succeeded, first.Outcome);
        Assert.Equal(1, first.ObservedCount);
        Assert.Equal("cursor-2", first.AcceptedCheckpoint);
        Assert.Equal(firstOperationId, Assert.Single(sink.Records).OperationId);
        Assert.Equal(first.ObservedCount, second.ObservedCount);
        Assert.All(handler.RequestUris, uri => Assert.Contains("checkpoint=cursor-1", uri.Query, StringComparison.Ordinal));
        Assert.All(handler.AuthorizationValues, value => Assert.Equal("Bearer private", value));
    }

    [Fact]
    public async Task Fails_run_when_durable_observations_cannot_advance_checkpoint()
    {
        RecordingHandler handler = new(ResponseJson);
        ServiceCollection services = new();
        services.AddFakeHttpAdapter().ConfigurePrimaryHttpMessageHandler(() => handler);
        await using ServiceProvider provider = services.BuildServiceProvider();
        IAdapterRunner runner = Assert.Single(provider.GetServices<IAdapterRunner>());
        using AdapterConfigurationMaterial material = CreateMaterial();

        AdapterRunCompletion completion = await runner.RunAsync(
            CreateAssignment("cursor-1"),
            material,
            new RecordingSink(checkpointAccepted: false),
            CancellationToken.None);

        Assert.Equal(AdapterRunOutcome.Failed, completion.Outcome);
        Assert.Equal("fake-http.checkpoint-not-accepted", completion.ErrorCode);
        Assert.Equal("cursor-1", completion.AcceptedCheckpoint);
    }

    private static AdapterRunAssignment CreateAssignment(string checkpoint)
    {
        DateTimeOffset now = new(2026, 7, 12, 12, 0, 0, TimeSpan.Zero);
        return new AdapterRunAssignment(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "tenant-a",
            Guid.NewGuid(),
            "fake.http",
            AdapterExecutionMode.Polling,
            now,
            now.AddMinutes(5),
            checkpoint);
    }

    private static AdapterConfigurationMaterial CreateMaterial() => new(
        schemaVersion: 1,
        "application/json",
        "{\"endpoint\":\"https://example.test/feed\",\"authorizationHeaderName\":\"Authorization\"}"u8,
        "application/json",
        "{\"authorizationHeaderValue\":\"Bearer private\"}"u8);

    private const string ResponseJson = """
        {
          "nextCheckpoint": "cursor-2",
          "records": [
            {
              "recordType": "reservation",
              "externalRecordId": "booking-42",
              "sourceRevision": "rev-2",
              "sourceUpdatedAtUtc": "2026-07-12T11:59:00Z",
              "observedAtUtc": "2026-07-12T12:00:00Z",
              "payload": { "status": "confirmed", "guest": "Test Guest" }
            }
          ]
        }
        """;

    private sealed class RecordingHandler(string responseJson) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];
        public List<string?> AuthorizationValues { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            this.RequestUris.Add(request.RequestUri!);
            this.AuthorizationValues.Add(request.Headers.Authorization?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class RecordingSink(bool checkpointAccepted) : IAdapterObservationSink
    {
        public List<AdapterObservedRecord> Records { get; } = [];

        public Task<AdapterObservationAcknowledgement> SubmitAsync(
            AdapterObservationSubmission submission,
            CancellationToken cancellationToken)
        {
            this.Records.AddRange(submission.Records);
            AdapterObservationResult[] results = submission.Records.Select(record =>
                new AdapterObservationResult(
                    record.OperationId,
                    AdapterObservationDisposition.Accepted,
                    Guid.NewGuid(),
                    errorCode: null)).ToArray();
            return Task.FromResult(new AdapterObservationAcknowledgement(
                submission.RunId,
                submission.LeaseId,
                results,
                checkpointAccepted,
                checkpointAccepted ? submission.ProposedCheckpoint : null));
        }
    }
}
