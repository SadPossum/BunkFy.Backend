namespace BunkFy.Modules.Ingestion.Application.Ingress;

using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.Ingestion.Application.Ports;
using BunkFy.Modules.Ingestion.Application.Security;
using Gma.Framework.Observability;
using Gma.Framework.RateLimiting;
using Gma.Framework.Scoping;
using Microsoft.Extensions.Options;

internal sealed class AdapterIngressGate(
    IAdapterIngressControlRepository controls,
    IMultiPartitionRateLimiter rateLimiter,
    IOptions<AdapterIngressQuotaOptions> options,
    IScopeContext scopeContext,
    ISecuritySignalRecorder securitySignals)
    : IAdapterIngressGate
{
    private static readonly TimeSpan Minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private readonly ISecuritySignalRecorder securitySignals = securitySignals;

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
            return this.Record(
                operation,
                AdapterIngressGateDecision.PolicyRejected(AdapterIngressPolicyRejection.ScopeMismatch));
        }

        AdapterIngressControlSnapshot? snapshot = await controls.ReadAdmissionAsync(cancellationToken)
            .ConfigureAwait(false);
        if (snapshot is null)
        {
            return this.Record(
                operation,
                AdapterIngressGateDecision.ProviderUnavailable());
        }

        if (snapshot.IsGlobalStopped)
        {
            return this.Record(
                operation,
                AdapterIngressGateDecision.PolicyRejected(AdapterIngressPolicyRejection.GlobalStopped));
        }

        if (snapshot.IsTenantSuspended)
        {
            return this.Record(
                operation,
                AdapterIngressGateDecision.PolicyRejected(AdapterIngressPolicyRejection.TenantSuspended));
        }

        if (!consumeQuota)
        {
            return this.Record(
                operation,
                AdapterIngressGateDecision.Allowed());
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
                this.Record(operation, AdapterIngressGateDecision.Allowed()),
            MultiPartitionRateLimitOutcome.Rejected when decision.RetryAfter.HasValue =>
                this.Record(
                    operation,
                    AdapterIngressGateDecision.QuotaRejected(
                        decision.RetryAfter.Value)),
            _ => this.Record(
                operation,
                AdapterIngressGateDecision.ProviderUnavailable())
        };
    }

    private AdapterIngressGateDecision Record(
        AdapterIngressOperation operation,
        AdapterIngressGateDecision decision)
    {
        AdapterIngressMetrics.Record(operation, decision.Outcome);
        SecuritySignalDefinition? signal =
            IngestionSecuritySignalDefinitions.ForDecision(decision);
        if (signal is not null)
        {
            this.securitySignals.Record(signal);
        }

        return decision;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
}
