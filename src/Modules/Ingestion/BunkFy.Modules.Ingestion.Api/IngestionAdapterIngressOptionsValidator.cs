namespace BunkFy.Modules.Ingestion.Api;

using Gma.Framework.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal sealed class IngestionAdapterIngressOptionsValidator(
    IHostEnvironment environment,
    IRateLimitProviderRegistration quotaProvider)
    : IValidateOptions<IngestionAdapterIngressOptions>
{
    public ValidateOptionsResult Validate(string? name, IngestionAdapterIngressOptions options)
    {
        if (environment.IsProduction() && options.Enabled && !quotaProvider.IsDistributed)
        {
            return ValidateOptionsResult.Fail(
                "Ingestion:AdapterIngress:Enabled requires a distributed quota provider in Production.");
        }

        return ValidateOptionsResult.Success;
    }
}
