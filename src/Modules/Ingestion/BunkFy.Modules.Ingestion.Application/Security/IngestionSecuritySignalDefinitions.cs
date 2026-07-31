namespace BunkFy.Modules.Ingestion.Application.Security;

using BunkFy.Modules.Ingestion.Application.Ports;
using Gma.Framework.Observability;

internal sealed class IngestionSecuritySignalDefinitions
    : ISecuritySignalDefinitionSource
{
    public static readonly SecuritySignalDefinition AdapterScopeRejected = new(
        "ingestion.adapter-scope-rejected",
        SecuritySignalCategory.Integration,
        SecuritySignalSeverity.Warning);

    public static readonly SecuritySignalDefinition AdapterQuotaRejected = new(
        "ingestion.adapter-quota-rejected",
        SecuritySignalCategory.Integration,
        SecuritySignalSeverity.Warning);

    public static readonly SecuritySignalDefinition
        AdapterAdmissionProviderUnavailable = new(
            "ingestion.adapter-admission-provider-unavailable",
            SecuritySignalCategory.Integration,
            SecuritySignalSeverity.Critical);

    public static readonly SecuritySignalDefinition AdapterGlobalStopEnforced = new(
        "ingestion.adapter-global-stop-enforced",
        SecuritySignalCategory.Integration,
        SecuritySignalSeverity.Warning);

    public static readonly SecuritySignalDefinition
        AdapterTenantSuspensionEnforced = new(
            "ingestion.adapter-tenant-suspension-enforced",
            SecuritySignalCategory.Integration,
            SecuritySignalSeverity.Warning);

    public static readonly SecuritySignalDefinition
        AdapterTenantLifecycleRestrictionEnforced = new(
            "ingestion.adapter-tenant-lifecycle-restriction-enforced",
            SecuritySignalCategory.Integration,
            SecuritySignalSeverity.Warning);

    private static readonly SecuritySignalDefinition[] All =
    [
        AdapterScopeRejected,
        AdapterQuotaRejected,
        AdapterAdmissionProviderUnavailable,
        AdapterGlobalStopEnforced,
        AdapterTenantSuspensionEnforced,
        AdapterTenantLifecycleRestrictionEnforced
    ];

    public IReadOnlyCollection<SecuritySignalDefinition> Definitions => All;

    public static SecuritySignalDefinition? ForDecision(
        AdapterIngressGateDecision decision) =>
        decision.Outcome switch
        {
            AdapterIngressGateOutcome.QuotaRejected =>
                AdapterQuotaRejected,
            AdapterIngressGateOutcome.ProviderUnavailable =>
                AdapterAdmissionProviderUnavailable,
            AdapterIngressGateOutcome.PolicyRejected =>
                decision.PolicyRejection switch
                {
                    AdapterIngressPolicyRejection.ScopeMismatch =>
                        AdapterScopeRejected,
                    AdapterIngressPolicyRejection.GlobalStopped =>
                        AdapterGlobalStopEnforced,
                    AdapterIngressPolicyRejection.TenantSuspended =>
                        AdapterTenantSuspensionEnforced,
                    AdapterIngressPolicyRejection
                        .TenantLifecycleRestricted =>
                        AdapterTenantLifecycleRestrictionEnforced,
                    _ => null
                },
            _ => null
        };
}
