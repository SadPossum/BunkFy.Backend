namespace BunkFy.Modules.Ingestion.Application.Ingress;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Ingestion.Application.Ports;
using Gma.Framework.RateLimiting;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Options;

internal sealed class AdapterIngressGate(
    IAdapterIngressControlRepository controls,
    IMultiPartitionRateLimiter rateLimiter,
    IOptions<AdapterIngressQuotaOptions> options,
    IScopeContext scopeContext)
    : IAdapterIngressGate
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);

    public async ValueTask<AdapterIngressGateDecision> AdmitAsync(
        AdapterIngressIdentity identity,
        AdapterIngressOperation operation,
        int permitCount,
        bool consumeQuota,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId) ||
            !string.Equals(scopeContext.ScopeId.Trim(), identity.ScopeId, StringComparison.Ordinal))
        {
            return Record(
                operation,
                AdapterIngressGateDecision.PolicyRejected(AdapterIngressPolicyRejection.ScopeMismatch));
        }

        AdapterIngressControlSnapshot? snapshot = await controls.ReadAdmissionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return Record(operation, AdapterIngressGateDecision.ProviderUnavailable());
        }

        if (snapshot.IsGlobalStopped)
        {
            return Record(
                operation,
                AdapterIngressGateDecision.PolicyRejected(AdapterIngressPolicyRejection.GlobalStopped));
        }

        if (snapshot.IsTenantSuspended)
        {
            return Record(
                operation,
                AdapterIngressGateDecision.PolicyRejected(AdapterIngressPolicyRejection.TenantSuspended));
        }

        if (!consumeQuota)
        {
            return Record(operation, AdapterIngressGateDecision.Allowed());
        }

        AdapterIngressQuotaOptions quota = options.Value;
        string tenantHash = Hash(identity.ScopeId);
        string credentialHash = Hash($"{identity.ScopeId}|{identity.CredentialId:N}");
        MultiPartitionRateLimitRequest request = new(
            $"bunkfy-adapter-ingress-{tenantHash}",
            permitCount,
            [
                new($"credential-minute-{credentialHash}", quota.CredentialPerMinute, Minute),
                new($"credential-hour-{credentialHash}", quota.CredentialPerHour, Hour),
                new("tenant-minute", quota.TenantPerMinute, Minute),
                new("tenant-hour", quota.TenantPerHour, Hour)
            ]);
        MultiPartitionRateLimitDecision decision = await rateLimiter.AcquireAsync(request, cancellationToken)
            .ConfigureAwait(false);
        return decision.Outcome switch
        {
            MultiPartitionRateLimitOutcome.Acquired =>
                Record(operation, AdapterIngressGateDecision.Allowed()),
            MultiPartitionRateLimitOutcome.Rejected when decision.RetryAfter.HasValue =>
                Record(operation, AdapterIngressGateDecision.QuotaRejected(decision.RetryAfter.Value)),
            _ => Record(operation, AdapterIngressGateDecision.ProviderUnavailable())
        };
    }

    private static AdapterIngressGateDecision Record(
        AdapterIngressOperation operation,
        AdapterIngressGateDecision decision)
    {
        AdapterIngressMetrics.Record(operation, decision.Outcome);
        return decision;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
