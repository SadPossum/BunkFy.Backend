namespace BunkFy.Adapters.Http;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BunkFy.Adapter.Abstractions;

public sealed class AdapterHttpIngressClient : IAdapterPushObservationSink, IAdapterRemoteControlClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly AdapterHttpIngressOptions options;
    private readonly IAdapterIngressTokenProvider tokenProvider;
    private readonly TimeProvider timeProvider;

    public AdapterHttpIngressClient(
        HttpClient httpClient,
        AdapterHttpIngressOptions options,
        IAdapterIngressTokenProvider tokenProvider,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        this.httpClient = httpClient;
        this.options = options;
        this.tokenProvider = tokenProvider;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AdapterIngressSubmissionResponse> SubmitAsync(
        IReadOnlyCollection<AdapterObservedRecord> records,
        CancellationToken cancellationToken)
    {
        AdapterIngressSubmissionRequest submission = CreateSubmission(records);
        HashSet<Guid> submittedOperations = records.Select(record => record.OperationId).ToHashSet();

        for (int attempt = 1; attempt <= this.options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string token = await this.tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
            AdapterIngressTokenRules.Validate(token);
            TimeSpan? retryDelay = null;

            try
            {
                using HttpRequestMessage request = this.CreateRequest(submission, token);
                using HttpResponseMessage response = await this.httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await ReadAndValidateResponseAsync(
                        response, submittedOperations, cancellationToken).ConfigureAwait(false);
                }

                if (attempt < this.options.MaxAttempts && IsTransient(response.StatusCode))
                {
                    retryDelay = this.ResolveRetryDelay(response, attempt);
                }
                else
                {
                    throw new HttpRequestException(
                        "The adapter ingress endpoint rejected the request.",
                        inner: null,
                        response.StatusCode);
                }
            }
            catch (HttpRequestException exception) when (
                exception.StatusCode is null && attempt < this.options.MaxAttempts)
            {
                retryDelay = this.CalculateBackoff(attempt);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested && attempt < this.options.MaxAttempts)
            {
                retryDelay = this.CalculateBackoff(attempt);
            }

            if (retryDelay.HasValue)
            {
                await Task.Delay(retryDelay.Value, this.timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Adapter ingress retry accounting reached an invalid state.");
    }

    public Task<AdapterRemoteLeaseClaimResponse> ClaimAsync(
        AdapterRemoteLeaseClaimRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ClaimId == Guid.Empty || request.WorkerId == Guid.Empty || request.ProtocolVersion <= 0 ||
            request.ConfigurationSchemaVersion <= 0 ||
            request.RequestedLeaseSeconds is < AdapterRemoteLeaseContractLimits.MinimumLeaseSeconds or
                > AdapterRemoteLeaseContractLimits.MaximumLeaseSeconds)
        {
            throw new ArgumentException("The remote lease claim is invalid.", nameof(request));
        }

        string adapterType = request.AdapterType?.Trim() ?? string.Empty;
        if (adapterType.Length is 0 or > AdapterProtocolLimits.AdapterTypeMaxLength ||
            !char.IsLetterOrDigit(adapterType[0]) || adapterType.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new ArgumentException("The remote adapter type is invalid.", nameof(request));
        }
        return this.SendRemoteAsync<AdapterRemoteLeaseClaimRequest, AdapterRemoteLeaseClaimResponse>(
            this.options.RemoteLeaseClaimEndpoint,
            request,
            response => response.Assignment is not null && response.Assignment.ConnectionId == this.options.ConnectionId &&
                response.Assignment.LeaseId != Guid.Empty && response.LeaseEpoch > 0 &&
                HasValidRenewalWindow(
                    response.Assignment.LeaseExpiresAtUtc,
                    response.RenewAfterSeconds,
                    this.timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public Task<AdapterRemoteLeaseRenewResponse> RenewAsync(
        AdapterRemoteLeaseRenewRequest request,
        CancellationToken cancellationToken)
    {
        ValidateLeaseProof(request?.Lease, nameof(request));
        if (request!.RequestedLeaseSeconds is < AdapterRemoteLeaseContractLimits.MinimumLeaseSeconds or
            > AdapterRemoteLeaseContractLimits.MaximumLeaseSeconds)
        {
            throw new ArgumentException("The remote lease renewal is invalid.", nameof(request));
        }

        return this.SendRemoteAsync<AdapterRemoteLeaseRenewRequest, AdapterRemoteLeaseRenewResponse>(
            this.options.RemoteLeaseRenewEndpoint,
            request,
            response => response.RunId == request.Lease.RunId && response.LeaseId == request.Lease.LeaseId &&
                response.LeaseEpoch == request.Lease.LeaseEpoch &&
                HasValidRenewalWindow(
                    response.LeaseExpiresAtUtc,
                    response.RenewAfterSeconds,
                    this.timeProvider.GetUtcNow()),
            cancellationToken);
    }

    public Task<AdapterRemoteObservationSubmissionResponse> SubmitAsync(
        AdapterRemoteObservationSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateLeaseProof(request?.Lease, nameof(request));
        if (request!.Records is null)
        {
            throw new ArgumentException("The remote observation records are required.", nameof(request));
        }

        AdapterIngressSubmissionRequest submission = CreateSubmission(
            request.Records.Select(record => new AdapterObservedRecord(
                record.OperationId,
                record.RecordType,
                record.ExternalRecordId,
                record.SourceRevision,
                record.SourceUpdatedAtUtc,
                record.ObservedAtUtc,
                record.ContentType,
                record.Payload,
                record.ContentSha256)).ToArray());
        if (request.ProposedCheckpoint?.Length > AdapterProtocolLimits.CheckpointMaxLength)
        {
            throw new ArgumentException("The remote checkpoint exceeds the protocol limit.", nameof(request));
        }

        HashSet<Guid> operationIds = submission.Records.Select(record => record.OperationId).ToHashSet();
        return this.SendRemoteAsync<AdapterRemoteObservationSubmissionRequest, AdapterRemoteObservationSubmissionResponse>(
            this.options.RemoteLeaseObservationsEndpoint,
            request,
            response => MatchesAcknowledgement(response.Acknowledgement, request.Lease, operationIds),
            cancellationToken);
    }

    public Task<AdapterRemoteRunCompletionResponse> CompleteAsync(
        AdapterRemoteRunCompletionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateLeaseProof(request?.Lease, nameof(request));
        if (request!.Outcome == AdapterRunOutcome.Unknown || !Enum.IsDefined(request.Outcome) ||
            request.ObservedCount < 0 || request.AcceptedCount < 0 || request.RejectedCount < 0 ||
            (long)request.AcceptedCount + request.RejectedCount > request.ObservedCount ||
            request.AcceptedCheckpoint?.Length > AdapterProtocolLimits.CheckpointMaxLength ||
            request.ErrorCode?.Length > AdapterProtocolLimits.ErrorCodeMaxLength)
        {
            throw new ArgumentException("The remote run completion is invalid.", nameof(request));
        }

        return this.SendRemoteAsync<AdapterRemoteRunCompletionRequest, AdapterRemoteRunCompletionResponse>(
            this.options.RemoteLeaseCompleteEndpoint,
            request,
            response => response.RunId == request.Lease.RunId && response.LeaseId == request.Lease.LeaseId &&
                response.LeaseEpoch == request.Lease.LeaseEpoch && response.Outcome == request.Outcome,
            cancellationToken);
    }

    private async Task<TResponse> SendRemoteAsync<TRequest, TResponse>(
        Uri endpoint,
        TRequest content,
        Func<TResponse, bool> validate,
        CancellationToken cancellationToken)
        where TRequest : class
        where TResponse : class
    {
        for (int attempt = 1; attempt <= this.options.MaxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string token = await this.tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);
            AdapterIngressTokenRules.Validate(token);
            TimeSpan? retryDelay = null;
            try
            {
                using HttpRequestMessage request = this.CreateRequest(endpoint, content, token);
                using HttpResponseMessage response = await this.httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return await ReadRemoteResponseAsync(response, validate, cancellationToken).ConfigureAwait(false);
                }

                if (attempt < this.options.MaxAttempts && IsTransient(response.StatusCode))
                {
                    retryDelay = this.ResolveRetryDelay(response, attempt);
                }
                else
                {
                    throw new HttpRequestException(
                        "The remote adapter control endpoint rejected the request.",
                        inner: null,
                        response.StatusCode);
                }
            }
            catch (HttpRequestException exception) when (
                exception.StatusCode is null && attempt < this.options.MaxAttempts)
            {
                retryDelay = this.CalculateBackoff(attempt);
            }
            catch (OperationCanceledException) when (
                !cancellationToken.IsCancellationRequested && attempt < this.options.MaxAttempts)
            {
                retryDelay = this.CalculateBackoff(attempt);
            }

            if (retryDelay.HasValue)
            {
                await Task.Delay(retryDelay.Value, this.timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new InvalidOperationException("Remote adapter control retry accounting reached an invalid state.");
    }

    private HttpRequestMessage CreateRequest(AdapterIngressSubmissionRequest submission, string token)
    {
        HttpRequestMessage request = new(HttpMethod.Post, this.options.Endpoint)
        {
            Content = JsonContent.Create(submission, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("BunkFy-Adapter", token);
        request.Headers.Add("X-Tenant-Id", this.options.TenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private HttpRequestMessage CreateRequest<TRequest>(Uri endpoint, TRequest content, string token)
    {
        HttpRequestMessage request = new(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(content, options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("BunkFy-Adapter", token);
        request.Headers.Add("X-Tenant-Id", this.options.TenantId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static AdapterIngressSubmissionRequest CreateSubmission(
        IReadOnlyCollection<AdapterObservedRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        AdapterObservedRecord[] copied = records.ToArray();
        if (copied.Length is 0 or > AdapterProtocolLimits.MaximumRecordsPerSubmission ||
            copied.Any(record => record is null) ||
            copied.Select(record => record.OperationId).Distinct().Count() != copied.Length ||
            copied.Sum(record => (long)record.Payload.Length) > AdapterProtocolLimits.MaximumSubmissionPayloadBytes)
        {
            throw new ArgumentException(
                "The adapter ingress submission exceeds the protocol record or payload limits.",
                nameof(records));
        }

        return new AdapterIngressSubmissionRequest(
            copied.Select(AdapterIngressObservationRequest.FromRecord).ToArray());
    }

    private static async Task<AdapterIngressSubmissionResponse> ReadAndValidateResponseAsync(
        HttpResponseMessage response,
        HashSet<Guid> submittedOperations,
        CancellationToken cancellationToken)
    {
        try
        {
            await response.Content.LoadIntoBufferAsync(
                AdapterIngressContractLimits.MaximumResponseBodyBytes,
                cancellationToken).ConfigureAwait(false);
            AdapterIngressSubmissionResponse? acknowledgement = await response.Content
                .ReadFromJsonAsync<AdapterIngressSubmissionResponse>(JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            if (acknowledgement?.Results is null ||
                acknowledgement.Results.Count != submittedOperations.Count ||
                acknowledgement.Results.Select(result => result.OperationId).Distinct().Count() !=
                    acknowledgement.Results.Count ||
                acknowledgement.Results.Any(result => !submittedOperations.Contains(result.OperationId)))
            {
                throw new InvalidDataException("The adapter ingress acknowledgement does not match the submission.");
            }

            return acknowledgement;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The adapter ingress acknowledgement is not valid JSON.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidDataException("The adapter ingress acknowledgement exceeds the allowed size.", exception);
        }
    }

    private static async Task<TResponse> ReadRemoteResponseAsync<TResponse>(
        HttpResponseMessage response,
        Func<TResponse, bool> validate,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        try
        {
            await response.Content.LoadIntoBufferAsync(
                AdapterIngressContractLimits.MaximumResponseBodyBytes,
                cancellationToken).ConfigureAwait(false);
            TResponse? body = await response.Content.ReadFromJsonAsync<TResponse>(
                JsonOptions, cancellationToken).ConfigureAwait(false);
            return body is not null && validate(body)
                ? body
                : throw new InvalidDataException("The remote adapter control response is inconsistent.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The remote adapter control response is not valid JSON.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidDataException("The remote adapter control response exceeds the allowed size.", exception);
        }
    }

    private static bool HasValidRenewalWindow(
        DateTimeOffset leaseExpiresAtUtc,
        int renewAfterSeconds,
        DateTimeOffset nowUtc) =>
        renewAfterSeconds > 0 &&
        leaseExpiresAtUtc > nowUtc &&
        leaseExpiresAtUtc - nowUtc > TimeSpan.FromSeconds(renewAfterSeconds);

    private static void ValidateLeaseProof(AdapterRemoteLeaseProof? lease, string parameterName)
    {
        if (lease is null || lease.RunId == Guid.Empty || lease.LeaseId == Guid.Empty ||
            lease.LeaseEpoch <= 0 || lease.WorkerId == Guid.Empty)
        {
            throw new ArgumentException("The remote adapter lease proof is invalid.", parameterName);
        }
    }

    private static bool MatchesAcknowledgement(
        AdapterObservationAcknowledgement? acknowledgement,
        AdapterRemoteLeaseProof lease,
        HashSet<Guid> operationIds) =>
        acknowledgement is not null && acknowledgement.RunId == lease.RunId &&
        acknowledgement.LeaseId == lease.LeaseId && acknowledgement.Results.Count == operationIds.Count &&
        acknowledgement.Results.Select(result => result.OperationId).Distinct().Count() == operationIds.Count &&
        acknowledgement.Results.All(result => operationIds.Contains(result.OperationId));

    private TimeSpan ResolveRetryDelay(HttpResponseMessage response, int attempt)
    {
        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
        if (!retryAfter.HasValue && response.Headers.RetryAfter?.Date is { } retryAt)
        {
            retryAfter = retryAt - this.timeProvider.GetUtcNow();
        }

        return retryAfter.HasValue && retryAfter.Value > TimeSpan.Zero
            ? Min(retryAfter.Value, this.options.RetryMaxDelay)
            : this.CalculateBackoff(attempt);
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        double exponentialMilliseconds = this.options.RetryBaseDelay.TotalMilliseconds *
            Math.Pow(2, attempt - 1);
        double boundedMilliseconds = Math.Min(exponentialMilliseconds, this.options.RetryMaxDelay.TotalMilliseconds);
        double jitterMultiplier = this.options.RetryJitterFactor == 0
            ? 1
            : 1 + (((Random.Shared.NextDouble() * 2) - 1) * this.options.RetryJitterFactor);
        return TimeSpan.FromMilliseconds(Math.Max(1, boundedMilliseconds * jitterMultiplier));
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.RequestTimeout or
        HttpStatusCode.TooManyRequests or
        HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or
        HttpStatusCode.GatewayTimeout;

    private static TimeSpan Min(TimeSpan left, TimeSpan right) => left <= right ? left : right;
}
