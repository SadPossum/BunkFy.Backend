namespace BunkFy.Adapters.FakeHttp;

using System.Buffers;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BunkFy.Adapter.Abstractions;

internal sealed class FakeHttpAdapterRunner(IHttpClientFactory httpClientFactory) : IAdapterRunner
{
    public const string AdapterType = "fake.http";
    private const int MaximumResponseBytes = 4 * 1024 * 1024;
    private const int CopyBufferBytes = 64 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public AdapterDescriptor Descriptor => FakeHttpAdapterDescriptor.Value;

    public async Task<AdapterRunCompletion> RunAsync(
        AdapterRunAssignment assignment,
        AdapterConfigurationMaterial material,
        IAdapterObservationSink sink,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(sink);
        if (assignment.AdapterType != AdapterType || assignment.ExecutionMode != AdapterExecutionMode.Polling)
        {
            throw new InvalidOperationException("The fake HTTP runner received an incompatible assignment.");
        }

        if (material.SchemaVersion != this.Descriptor.ConfigurationSchemaVersion ||
            !string.Equals(material.ConfigurationContentType, "application/json", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The fake HTTP adapter configuration material is incompatible.");
        }

        FakeHttpConfiguration configuration = Deserialize<FakeHttpConfiguration>(material.Configuration.Span);
        Uri endpoint = ValidateEndpoint(configuration.Endpoint);
        FakeHttpSecret? secret = material.HasSecret
            ? Deserialize<FakeHttpSecret>(material.Secret.Span)
            : null;

        using HttpRequestMessage request = new(HttpMethod.Get, AddCheckpoint(endpoint, assignment.Checkpoint));
        AddAuthorization(request.Headers, configuration.AuthorizationHeaderName, secret);

        HttpClient client = httpClientFactory.CreateClient(AdapterType);
        using HttpResponseMessage response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentType?.MediaType is not "application/json")
        {
            throw new InvalidOperationException("The fake HTTP response must use application/json.");
        }

        byte[] responseBytes = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
        FakeHttpPage page = Deserialize<FakeHttpPage>(responseBytes);
        IReadOnlyList<FakeHttpRecord> records = page.Records ?? throw new InvalidOperationException(
            "The fake HTTP response requires a records array.");
        if (records.Count > AdapterProtocolLimits.MaximumRecordsPerSubmission)
        {
            throw new InvalidOperationException("The fake HTTP response contains too many records.");
        }

        if (records.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(page.NextCheckpoint) &&
                !string.Equals(page.NextCheckpoint, assignment.Checkpoint, StringComparison.Ordinal))
            {
                return Failed(assignment, "fake-http.empty-page-checkpoint", "An empty page cannot advance the checkpoint.");
            }

            return new AdapterRunCompletion(
                assignment.RunId,
                assignment.LeaseId,
                AdapterRunOutcome.Succeeded,
                observedCount: 0,
                acceptedCount: 0,
                rejectedCount: 0,
                assignment.Checkpoint,
                errorCode: null,
                errorMessage: null);
        }

        AdapterObservedRecord[] observations = records.Select(record => MapRecord(assignment, record)).ToArray();
        string? proposedCheckpoint = NormalizeOptional(page.NextCheckpoint);
        AdapterObservationAcknowledgement acknowledgement = await sink.SubmitAsync(
            new AdapterObservationSubmission(
                assignment.RunId,
                assignment.LeaseId,
                observations,
                proposedCheckpoint),
            cancellationToken).ConfigureAwait(false);

        if (acknowledgement.RunId != assignment.RunId || acknowledgement.LeaseId != assignment.LeaseId)
        {
            return Failed(assignment, "fake-http.acknowledgement-mismatch", "The receipt acknowledgement did not match the assignment.");
        }

        int rejected = acknowledgement.Results.Count(result =>
            result.Disposition == AdapterObservationDisposition.Rejected);
        int accepted = acknowledgement.Results.Count - rejected;
        if (proposedCheckpoint is not null && !acknowledgement.CheckpointAccepted && rejected == 0)
        {
            return new AdapterRunCompletion(
                assignment.RunId,
                assignment.LeaseId,
                AdapterRunOutcome.Failed,
                observations.Length,
                accepted,
                rejected,
                assignment.Checkpoint,
                "fake-http.checkpoint-not-accepted",
                "The observations were durable but their checkpoint was not accepted; replay is required.");
        }

        return new AdapterRunCompletion(
            assignment.RunId,
            assignment.LeaseId,
            rejected == 0 ? AdapterRunOutcome.Succeeded : AdapterRunOutcome.PartiallySucceeded,
            observations.Length,
            accepted,
            rejected,
            acknowledgement.AcceptedCheckpoint ?? assignment.Checkpoint,
            rejected == 0 ? null : "fake-http.observation-rejected",
            rejected == 0 ? null : "One or more observations were rejected before durable receipt.");
    }

    private static AdapterObservedRecord MapRecord(AdapterRunAssignment assignment, FakeHttpRecord record)
    {
        if (record.Payload.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("A fake HTTP record payload is required.");
        }

        if (record.ObservedAtUtc == default)
        {
            throw new InvalidOperationException("A fake HTTP record observation timestamp is required.");
        }

        byte[] payload = Encoding.UTF8.GetBytes(record.Payload.GetRawText());
        string contentHash = AdapterPayloadHash.ComputeSha256(payload);
        return new AdapterObservedRecord(
            CreateOperationId(assignment.ConnectionId, record, contentHash),
            record.RecordType,
            record.ExternalRecordId,
            NormalizeOptional(record.SourceRevision),
            record.SourceUpdatedAtUtc,
            record.ObservedAtUtc,
            "application/json",
            payload,
            contentHash);
    }

    private static Guid CreateOperationId(Guid connectionId, FakeHttpRecord record, string contentHash)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, connectionId.ToString("N"));
        Append(hash, record.RecordType);
        Append(hash, record.ExternalRecordId);
        Append(hash, NormalizeOptional(record.SourceRevision) ?? string.Empty);
        Append(hash, contentHash);
        byte[] digest = hash.GetHashAndReset();
        return new Guid(digest.AsSpan(0, 16));
    }

    private static void Append(IncrementalHash hash, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BitConverter.TryWriteBytes(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }

    private static T Deserialize<T>(ReadOnlySpan<byte> json)
        where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, SerializerOptions) ??
                throw new InvalidOperationException("The fake HTTP JSON document is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("The fake HTTP JSON document is invalid.", exception);
        }
    }

    private static Uri ValidateEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && (uri.Scheme != Uri.UriSchemeHttp || !uri.IsLoopback)) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("The fake HTTP endpoint must use HTTPS, or HTTP on loopback, without user info or a fragment.");
        }

        return uri;
    }

    private static Uri AddCheckpoint(Uri endpoint, string? checkpoint)
    {
        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            return endpoint;
        }

        UriBuilder builder = new(endpoint);
        string prefix = string.IsNullOrEmpty(builder.Query) ? string.Empty : $"{builder.Query[1..]}&";
        builder.Query = $"{prefix}checkpoint={Uri.EscapeDataString(checkpoint)}";
        return builder.Uri;
    }

    private static void AddAuthorization(
        HttpRequestHeaders headers,
        string? headerName,
        FakeHttpSecret? secret)
    {
        if (string.IsNullOrWhiteSpace(headerName) && secret is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(headerName) ||
            (!string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase) &&
             !headerName.StartsWith("X-", StringComparison.OrdinalIgnoreCase)) ||
            string.IsNullOrWhiteSpace(secret?.AuthorizationHeaderValue) ||
            secret.AuthorizationHeaderValue.Contains('\r') ||
            secret.AuthorizationHeaderValue.Contains('\n') ||
            !headers.TryAddWithoutValidation(headerName, secret.AuthorizationHeaderValue))
        {
            throw new InvalidOperationException("The fake HTTP authorization material is invalid.");
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumResponseBytes)
        {
            throw new InvalidOperationException("The fake HTTP response exceeds the configured size limit.");
        }

        await using Stream source = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using MemoryStream destination = new();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(CopyBufferBytes);
        try
        {
            while (true)
            {
                int read = await source.ReadAsync(buffer.AsMemory(0, CopyBufferBytes), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (destination.Length + read > MaximumResponseBytes)
                {
                    throw new InvalidOperationException("The fake HTTP response exceeds the configured size limit.");
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }

            return destination.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static AdapterRunCompletion Failed(
        AdapterRunAssignment assignment,
        string code,
        string message) =>
        new(
            assignment.RunId,
            assignment.LeaseId,
            AdapterRunOutcome.Failed,
            observedCount: 0,
            acceptedCount: 0,
            rejectedCount: 0,
            assignment.Checkpoint,
            code,
            message);

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed class FakeHttpConfiguration
    {
        [JsonPropertyName("endpoint")]
        public string Endpoint { get; init; } = string.Empty;

        [JsonPropertyName("authorizationHeaderName")]
        public string? AuthorizationHeaderName { get; init; }
    }

    private sealed class FakeHttpSecret
    {
        [JsonPropertyName("authorizationHeaderValue")]
        public string? AuthorizationHeaderValue { get; init; }
    }

    private sealed class FakeHttpPage
    {
        [JsonPropertyName("nextCheckpoint")]
        public string? NextCheckpoint { get; init; }

        [JsonPropertyName("records")]
        public List<FakeHttpRecord>? Records { get; init; }
    }

    private sealed class FakeHttpRecord
    {
        [JsonPropertyName("recordType")]
        public string RecordType { get; init; } = string.Empty;

        [JsonPropertyName("externalRecordId")]
        public string ExternalRecordId { get; init; } = string.Empty;

        [JsonPropertyName("sourceRevision")]
        public string? SourceRevision { get; init; }

        [JsonPropertyName("sourceUpdatedAtUtc")]
        public DateTimeOffset? SourceUpdatedAtUtc { get; init; }

        [JsonPropertyName("observedAtUtc")]
        public DateTimeOffset ObservedAtUtc { get; init; }

        [JsonPropertyName("payload")]
        public JsonElement Payload { get; init; }
    }
}
