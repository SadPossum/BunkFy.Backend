namespace BunkFy.Adapters.Tests;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;
using BunkFy.Adapters.Http;
using Xunit;

[Trait("Category", "Unit")]
public sealed class AdapterHttpIngressClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Guid ConnectionId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private const string TenantId = "tenant-a";
    private const string Token = "bfi_v1_secret-value";

    [Fact]
    public async Task Submits_wire_contract_without_exposing_token()
    {
        AdapterObservedRecord record = CreateRecord();
        RecordingHandler handler = new(_ => Success(record.OperationId));
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(httpClient, new StaticAdapterIngressTokenProvider(Token));

        AdapterIngressSubmissionResponse response = await client.SubmitAsync(
            [record], CancellationToken.None);

        Assert.Equal(AdapterObservationDisposition.Accepted, Assert.Single(response.Results).Disposition);
        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.Equal(
            $"https://backend.example.test/root/api/ingestion/adapter-ingress/connections/{ConnectionId:D}/observations",
            request.Uri.AbsoluteUri);
        Assert.Equal("BunkFy-Adapter", request.AuthorizationScheme);
        Assert.Equal(Token, request.AuthorizationParameter);
        Assert.Equal(TenantId, request.TenantId);
        Assert.DoesNotContain(Token, request.Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.DoesNotContain(Token, request.Body, StringComparison.Ordinal);
        AdapterIngressSubmissionRequest? submission = JsonSerializer.Deserialize<AdapterIngressSubmissionRequest>(
            request.Body,
            JsonOptions);
        Assert.Equal(record.OperationId, Assert.Single(submission!.Records).OperationId);
    }

    [Fact]
    public async Task Retries_transient_response_and_refreshes_token_each_attempt()
    {
        AdapterObservedRecord record = CreateRecord();
        RecordingHandler handler = new(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            _ => Success(record.OperationId));
        SequenceTokenProvider tokenProvider = new("bfi_v1_first", "bfi_v1_rotated");
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(httpClient, tokenProvider, maxAttempts: 2);

        AdapterIngressSubmissionResponse response = await client.SubmitAsync(
            [record], CancellationToken.None);

        Assert.Equal(AdapterObservationDisposition.Accepted, Assert.Single(response.Results).Disposition);
        Assert.Equal(2, tokenProvider.Calls);
        Assert.Equal(
            ["bfi_v1_first", "bfi_v1_rotated"],
            handler.Requests.Select(request => request.AuthorizationParameter));
    }

    [Fact]
    public async Task Retries_transport_failure_with_a_fresh_request()
    {
        AdapterObservedRecord record = CreateRecord();
        RecordingHandler handler = new(
            _ => throw new HttpRequestException("connection unavailable"),
            _ => Success(record.OperationId));
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(
            httpClient,
            new StaticAdapterIngressTokenProvider(Token),
            maxAttempts: 2);

        AdapterIngressSubmissionResponse response = await client.SubmitAsync(
            [record], CancellationToken.None);

        Assert.Equal(AdapterObservationDisposition.Accepted, Assert.Single(response.Results).Disposition);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Does_not_retry_permanent_rejection_or_include_secret_in_error()
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent($"rejected {Token}", Encoding.UTF8, "text/plain")
        });
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(
            httpClient,
            new StaticAdapterIngressTokenProvider(Token),
            maxAttempts: 3);

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.SubmitAsync([CreateRecord()], CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, exception.StatusCode);
        Assert.DoesNotContain(Token, exception.ToString(), StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Rejects_invalid_batch_before_acquiring_token_or_sending()
    {
        AdapterObservedRecord record = CreateRecord();
        RecordingHandler handler = new(_ => throw new InvalidOperationException("must not send"));
        SequenceTokenProvider tokenProvider = new(Token);
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(httpClient, tokenProvider);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.SubmitAsync([record, record], CancellationToken.None));

        Assert.Equal(0, tokenProvider.Calls);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Rejects_acknowledgement_for_another_operation()
    {
        RecordingHandler handler = new(_ => Success(Guid.NewGuid()));
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(httpClient, new StaticAdapterIngressTokenProvider(Token));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            client.SubmitAsync([CreateRecord()], CancellationToken.None));
    }

    [Fact]
    public async Task Honors_caller_cancellation_before_secret_access()
    {
        RecordingHandler handler = new(_ => throw new InvalidOperationException("must not send"));
        SequenceTokenProvider tokenProvider = new(Token);
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(httpClient, tokenProvider);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.SubmitAsync([CreateRecord()], cancellation.Token));

        Assert.Equal(0, tokenProvider.Calls);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Claims_remote_assignment_with_fresh_token_and_validates_identity()
    {
        Guid workerId = Guid.NewGuid();
        Guid runId = Guid.NewGuid();
        Guid leaseId = Guid.NewGuid();
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AdapterRemoteLeaseClaimResponse(
                new AdapterRunAssignment(
                    runId,
                    leaseId,
                    ConnectionId,
                    TenantId,
                    Guid.Parse("10000000-0000-0000-0000-000000000001"),
                    "fake.http",
                    AdapterExecutionMode.Polling,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddMinutes(2),
                    "checkpoint-1"),
                LeaseEpoch: 3,
                RenewAfterSeconds: 30))
        });
        SequenceTokenProvider tokens = new(Token);
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(httpClient, tokens);

        AdapterRemoteLeaseClaimResponse response = await client.ClaimAsync(
            new(Guid.NewGuid(), workerId, "fake.http", 1, 1, 120),
            CancellationToken.None);

        Assert.Equal(runId, response.Assignment.RunId);
        Assert.Equal(3, response.LeaseEpoch);
        RecordedRequest request = Assert.Single(handler.Requests);
        Assert.EndsWith($"/{ConnectionId:D}/remote-leases/claim", request.Uri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Equal(Token, request.AuthorizationParameter);
        Assert.DoesNotContain(Token, request.Body, StringComparison.Ordinal);
        Assert.Equal(1, tokens.Calls);
    }

    [Fact]
    public async Task Rejects_remote_assignment_that_schedules_renewal_after_expiry()
    {
        Guid workerId = Guid.NewGuid();
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AdapterRemoteLeaseClaimResponse(
                new AdapterRunAssignment(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    ConnectionId,
                    TenantId,
                    Guid.NewGuid(),
                    "fake.http",
                    AdapterExecutionMode.Polling,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddMinutes(1),
                    checkpoint: null),
                LeaseEpoch: 1,
                RenewAfterSeconds: 120))
        });
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(
            httpClient, new StaticAdapterIngressTokenProvider(Token));

        await Assert.ThrowsAsync<InvalidDataException>(() => client.ClaimAsync(
            new(Guid.NewGuid(), workerId, "fake.http", 1, 1, 60),
            CancellationToken.None));
    }

    [Fact]
    public async Task Submits_remote_checkpoint_contract_and_rejects_mismatched_acknowledgement()
    {
        AdapterObservedRecord record = CreateRecord();
        AdapterRemoteLeaseProof proof = new(Guid.NewGuid(), Guid.NewGuid(), 4, Guid.NewGuid());
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new AdapterRemoteObservationSubmissionResponse(
                new AdapterObservationAcknowledgement(
                    proof.RunId,
                    proof.LeaseId,
                    [new AdapterObservationResult(
                        record.OperationId,
                        AdapterObservationDisposition.Accepted,
                        Guid.NewGuid(),
                        errorCode: null)],
                    checkpointAccepted: true,
                    "checkpoint-2")))
        });
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(
            httpClient, new StaticAdapterIngressTokenProvider(Token));

        AdapterRemoteObservationSubmissionResponse response = await client.SubmitAsync(
            new AdapterRemoteObservationSubmissionRequest(
                proof,
                [AdapterIngressObservationRequest.FromRecord(record)],
                "checkpoint-2"),
            CancellationToken.None);

        Assert.True(response.Acknowledgement.CheckpointAccepted);
        Assert.EndsWith(
            $"/{ConnectionId:D}/remote-leases/observations",
            Assert.Single(handler.Requests).Uri.AbsoluteUri,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remote_lease_conflict_is_not_retried_or_rendered_with_response_body()
    {
        RecordingHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.Conflict)
        {
            Content = new StringContent($"lease owned; {Token}")
        });
        SequenceTokenProvider tokens = new(Token, "bfi_v1_unused");
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(httpClient, tokens, maxAttempts: 2);

        HttpRequestException exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ClaimAsync(
                new(Guid.NewGuid(), Guid.NewGuid(), "fake.http", 1, 1, 60),
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.Conflict, exception.StatusCode);
        Assert.DoesNotContain(Token, exception.ToString(), StringComparison.Ordinal);
        Assert.Single(handler.Requests);
        Assert.Equal(1, tokens.Calls);
    }

    [Fact]
    public async Task Rejects_overflowing_remote_completion_counts_before_secret_access()
    {
        RecordingHandler handler = new(_ => throw new InvalidOperationException("must not send"));
        SequenceTokenProvider tokens = new(Token);
        using HttpClient httpClient = new(handler);
        AdapterHttpIngressClient client = CreateClient(httpClient, tokens);
        AdapterRemoteLeaseProof proof = new(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid());

        await Assert.ThrowsAsync<ArgumentException>(() => client.CompleteAsync(
            new AdapterRemoteRunCompletionRequest(
                proof,
                AdapterRunOutcome.Succeeded,
                int.MaxValue,
                int.MaxValue,
                int.MaxValue,
                AcceptedCheckpoint: null,
                ErrorCode: null,
                ErrorMessage: null),
            CancellationToken.None));

        Assert.Equal(0, tokens.Calls);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Requires_https_except_for_explicit_loopback_and_redacts_static_provider()
    {
        Assert.Throws<ArgumentException>(() => new AdapterHttpIngressOptions(
            new Uri("http://backend.example.test"), TenantId, ConnectionId));
        Assert.Throws<ArgumentException>(() => new AdapterHttpIngressOptions(
            new Uri("https://user:password@backend.example.test"), TenantId, ConnectionId));
        AdapterHttpIngressOptions loopback = new(
            new Uri("http://localhost:5000"), TenantId, ConnectionId, allowInsecureLoopback: true);
        StaticAdapterIngressTokenProvider provider = new(Token);

        Assert.Equal(Uri.UriSchemeHttp, loopback.Endpoint.Scheme);
        Assert.Equal(nameof(StaticAdapterIngressTokenProvider), provider.ToString());
        Assert.DoesNotContain(Token, provider.ToString(), StringComparison.Ordinal);
    }

    private static AdapterHttpIngressClient CreateClient(
        HttpClient httpClient,
        IAdapterIngressTokenProvider tokenProvider,
        int maxAttempts = 1)
    {
        AdapterHttpIngressOptions options = new(
            new Uri("https://backend.example.test/root"),
            TenantId,
            ConnectionId,
            maxAttempts,
            TimeSpan.FromMilliseconds(1),
            TimeSpan.FromMilliseconds(1),
            retryJitterFactor: 0);
        return new AdapterHttpIngressClient(httpClient, options, tokenProvider);
    }

    private static AdapterObservedRecord CreateRecord(Guid? operationId = null)
    {
        byte[] payload = "{\"status\":\"confirmed\"}"u8.ToArray();
        return new AdapterObservedRecord(
            operationId ?? Guid.Parse("30000000-0000-0000-0000-000000000002"),
            "reservation",
            "booking-42",
            "revision-1",
            DateTimeOffset.Parse("2026-07-12T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
            DateTimeOffset.Parse("2026-07-12T10:01:00Z", System.Globalization.CultureInfo.InvariantCulture),
            "application/json",
            payload,
            AdapterPayloadHash.ComputeSha256(payload));
    }

    private static HttpResponseMessage Success(Guid operationId) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new AdapterIngressSubmissionResponse(
            [new AdapterObservationResult(
                operationId,
                AdapterObservationDisposition.Accepted,
                Guid.Parse("30000000-0000-0000-0000-000000000003"),
                errorCode: null)]))
    };

    private sealed class SequenceTokenProvider(params string[] tokens) : IAdapterIngressTokenProvider
    {
        public int Calls { get; private set; }

        public ValueTask<string> GetTokenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string token = tokens[Math.Min(this.Calls, tokens.Length - 1)];
            this.Calls++;
            return ValueTask.FromResult(token);
        }
    }

    private sealed class RecordingHandler(params Func<RecordedRequest, HttpResponseMessage>[] responses)
        : HttpMessageHandler
    {
        private int responseIndex;

        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RecordedRequest recorded = new(
                request.RequestUri!,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                Assert.Single(request.Headers.GetValues("X-Tenant-Id")),
                await request.Content!.ReadAsStringAsync(cancellationToken));
            this.Requests.Add(recorded);
            Func<RecordedRequest, HttpResponseMessage> response =
                responses[Math.Min(this.responseIndex, responses.Length - 1)];
            this.responseIndex++;
            return response(recorded);
        }
    }

    private sealed record RecordedRequest(
        Uri Uri,
        string? AuthorizationScheme,
        string? AuthorizationParameter,
        string TenantId,
        string Body);
}
