namespace BunkFy.Adapters.Http;

public sealed class AdapterHttpIngressOptions
{
    public const int MaximumAttempts = 10;
    public const int TenantIdMaxLength = 128;

    public AdapterHttpIngressOptions(
        Uri serviceBaseAddress,
        string tenantId,
        Guid connectionId,
        int maxAttempts = 4,
        TimeSpan? retryBaseDelay = null,
        TimeSpan? retryMaxDelay = null,
        double retryJitterFactor = 0.2,
        bool allowInsecureLoopback = false)
    {
        ArgumentNullException.ThrowIfNull(serviceBaseAddress);
        if (!serviceBaseAddress.IsAbsoluteUri ||
            !string.IsNullOrEmpty(serviceBaseAddress.UserInfo) ||
            !string.IsNullOrEmpty(serviceBaseAddress.Query) ||
            !string.IsNullOrEmpty(serviceBaseAddress.Fragment))
        {
            throw new ArgumentException(
                "The adapter ingress service address must be an absolute URI without user info, query, or fragment.",
                nameof(serviceBaseAddress));
        }

        bool secure = string.Equals(serviceBaseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        bool allowedLoopback = allowInsecureLoopback && serviceBaseAddress.IsLoopback &&
            string.Equals(serviceBaseAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);
        if (!secure && !allowedLoopback)
        {
            throw new ArgumentException(
                "Adapter ingress requires HTTPS; insecure HTTP is allowed only for an explicitly enabled loopback address.",
                nameof(serviceBaseAddress));
        }

        string normalizedTenantId = tenantId?.Trim() ?? string.Empty;
        if (normalizedTenantId.Length is 0 or > TenantIdMaxLength ||
            normalizedTenantId.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new ArgumentException("The adapter ingress tenant id is invalid.", nameof(tenantId));
        }

        if (connectionId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty adapter connection id is required.", nameof(connectionId));
        }

        TimeSpan selectedBaseDelay = retryBaseDelay ?? TimeSpan.FromSeconds(1);
        TimeSpan selectedMaxDelay = retryMaxDelay ?? TimeSpan.FromSeconds(30);
        if (maxAttempts is <= 0 or > MaximumAttempts ||
            selectedBaseDelay <= TimeSpan.Zero ||
            selectedMaxDelay < selectedBaseDelay ||
            selectedMaxDelay > TimeSpan.FromMinutes(5) ||
            retryJitterFactor is < 0 or > 1)
        {
            throw new ArgumentException("The adapter ingress retry options are invalid.");
        }

        this.ServiceBaseAddress = EnsureTrailingSlash(serviceBaseAddress);
        this.TenantId = normalizedTenantId;
        this.ConnectionId = connectionId;
        this.MaxAttempts = maxAttempts;
        this.RetryBaseDelay = selectedBaseDelay;
        this.RetryMaxDelay = selectedMaxDelay;
        this.RetryJitterFactor = retryJitterFactor;
        this.AllowInsecureLoopback = allowInsecureLoopback;
        this.Endpoint = new Uri(
            this.ServiceBaseAddress,
            $"api/ingestion/adapter-ingress/connections/{connectionId:D}/observations");
        string remoteLeaseBase =
            $"api/ingestion/adapter-ingress/connections/{connectionId:D}/remote-leases/";
        this.RemoteLeaseClaimEndpoint = new Uri(this.ServiceBaseAddress, remoteLeaseBase + "claim");
        this.RemoteLeaseRenewEndpoint = new Uri(this.ServiceBaseAddress, remoteLeaseBase + "renew");
        this.RemoteLeaseObservationsEndpoint = new Uri(this.ServiceBaseAddress, remoteLeaseBase + "observations");
        this.RemoteLeaseCompleteEndpoint = new Uri(this.ServiceBaseAddress, remoteLeaseBase + "complete");
    }

    public Uri ServiceBaseAddress { get; }
    public Uri Endpoint { get; }
    public Uri RemoteLeaseClaimEndpoint { get; }
    public Uri RemoteLeaseRenewEndpoint { get; }
    public Uri RemoteLeaseObservationsEndpoint { get; }
    public Uri RemoteLeaseCompleteEndpoint { get; }
    public string TenantId { get; }
    public Guid ConnectionId { get; }
    public int MaxAttempts { get; }
    public TimeSpan RetryBaseDelay { get; }
    public TimeSpan RetryMaxDelay { get; }
    public double RetryJitterFactor { get; }
    public bool AllowInsecureLoopback { get; }

    private static Uri EnsureTrailingSlash(Uri value) =>
        value.AbsoluteUri[^1] == '/'
            ? value
            : new Uri(value.AbsoluteUri + '/', UriKind.Absolute);
}
