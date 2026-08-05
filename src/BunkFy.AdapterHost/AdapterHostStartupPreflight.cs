namespace BunkFy.AdapterHost;

using BunkFy.Adapter.Abstractions;
using BunkFy.Adapter.Runtime;
using BunkFy.Adapters.Http;

internal sealed class AdapterHostStartupPreflight(
    IAdapterIngressTokenProvider tokenProvider,
    IAdapterRuntimeMaterialProvider materialProvider,
    AdapterRuntimeIdentity identity)
{
    private const int MaximumTokenLength = 512;

    public async Task ValidateAsync(
        AdapterDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        string token = await tokenProvider.GetTokenAsync(cancellationToken)
            .ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token) ||
            token.Length > MaximumTokenLength ||
            token.Any(character =>
                char.IsWhiteSpace(character) || char.IsControl(character)))
        {
            throw new InvalidOperationException(
                "The adapter ingress token source is invalid.");
        }

        using AdapterConfigurationMaterial material =
            await materialProvider.ResolveAsync(
                    identity,
                    descriptor.ConfigurationSchemaVersion,
                    cancellationToken)
                .ConfigureAwait(false);
        if (material.SchemaVersion != descriptor.ConfigurationSchemaVersion ||
            material.Configuration.IsEmpty)
        {
            throw new InvalidOperationException(
                "The adapter runtime material does not match the selected runner.");
        }
    }
}
