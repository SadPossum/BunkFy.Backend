namespace BunkFy.Modules.DataRights.Persistence;

using BunkFy.Modules.DataRights.Application.Ports;
using Microsoft.Extensions.Options;

internal sealed class DataRightsExportArtifactPolicy(
    IOptions<DataRightsExportArtifactOptions> options)
    : IDataRightsExportArtifactPolicy
{
    private readonly TimeSpan lifetime = options.Value.ArtifactLifetime;

    public DateTimeOffset ExpiresAt(DateTimeOffset requestedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
            requestedAtUtc,
            default,
            nameof(requestedAtUtc));
        return requestedAtUtc.Add(this.lifetime);
    }
}
