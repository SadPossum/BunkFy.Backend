namespace BunkFy.Modules.Ingestion.Application.Handlers;

using System.Globalization;
using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Application.Commands;

internal static class IngestionCredentialMutationFingerprint
{
    public static string ComputeCreate(
        CreateAdapterIngressCredentialCommand command,
        AdapterDescriptor descriptor,
        string sourceSystem) => IngestionMutationFingerprint.Compute(
        "bunkfy-ingestion-adapter-credential-create/v1",
        command.PropertyId.ToString("N"),
        command.ConnectionId.ToString("N"),
        descriptor.AdapterType,
        descriptor.ProtocolVersion.ToString(CultureInfo.InvariantCulture),
        descriptor.ConfigurationSchemaVersion.ToString(
            CultureInfo.InvariantCulture),
        command.Label?.Trim() ?? string.Empty,
        command.ExpiresAtUtc is { } expiresAtUtc
            ? expiresAtUtc.UtcDateTime.Ticks.ToString(
                CultureInfo.InvariantCulture)
            : "default",
        command.CreatedBy?.Trim() ?? string.Empty,
        sourceSystem.Trim().ToLowerInvariant());

    public static string ComputeRevoke(
        RevokeAdapterIngressCredentialCommand command) =>
        IngestionMutationFingerprint.Compute(
            "bunkfy-ingestion-adapter-credential-revoke/v1",
            command.PropertyId.ToString("N"),
            command.ConnectionId.ToString("N"),
            command.CredentialId.ToString("N"),
            command.ExpectedVersion.ToString(CultureInfo.InvariantCulture),
            command.RevokedBy?.Trim() ?? string.Empty);
}
