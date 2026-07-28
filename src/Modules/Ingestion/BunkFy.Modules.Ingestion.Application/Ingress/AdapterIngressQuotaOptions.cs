namespace BunkFy.Modules.Ingestion.Application.Ingress;

public sealed class AdapterIngressQuotaOptions
{
    public const string SectionName = "Ingestion:AdapterIngress:Quota";

    public int CredentialPerMinute { get; set; } = 120;
    public int CredentialPerHour { get; set; } = 2_000;
    public int TenantPerMinute { get; set; } = 1_000;
    public int TenantPerHour { get; set; } = 20_000;

    public static bool IsValid(AdapterIngressQuotaOptions options) =>
        options.CredentialPerMinute > 0 &&
        options.CredentialPerHour >= options.CredentialPerMinute &&
        options.TenantPerMinute >= options.CredentialPerMinute &&
        options.TenantPerHour >= options.CredentialPerHour &&
        options.CredentialPerMinute <= Gma.Framework.RateLimiting.FixedWindowRateLimitPartition.PermitLimitMaxValue &&
        options.CredentialPerHour <= Gma.Framework.RateLimiting.FixedWindowRateLimitPartition.PermitLimitMaxValue &&
        options.TenantPerMinute <= Gma.Framework.RateLimiting.FixedWindowRateLimitPartition.PermitLimitMaxValue &&
        options.TenantPerHour <= Gma.Framework.RateLimiting.FixedWindowRateLimitPartition.PermitLimitMaxValue;
}
