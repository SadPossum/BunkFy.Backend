namespace BunkFy.Modules.Ingestion.Application.Ingress;

using Gma.Framework.RateLimiting;

internal sealed class UnavailableAdapterIngressRateLimitProvider
    : IMultiPartitionRateLimiter,
      IRateLimitProviderRegistration
{
    public string ProviderName => "unavailable";
    public bool IsDistributed => false;

    public ValueTask<MultiPartitionRateLimitDecision> AcquireAsync(
        MultiPartitionRateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(MultiPartitionRateLimitDecision.ProviderUnavailable());
    }
}
