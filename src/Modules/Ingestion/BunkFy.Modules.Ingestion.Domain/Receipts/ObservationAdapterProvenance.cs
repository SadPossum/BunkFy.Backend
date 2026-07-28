namespace BunkFy.Modules.Ingestion.Domain.Receipts;

using BunkFy.Adapter.Abstractions;
using BunkFy.Modules.Ingestion.Domain.Credentials;
using BunkFy.Modules.Ingestion.Domain.Errors;
using Gma.Framework.Results;

public sealed class ObservationAdapterProvenance
{
    private ObservationAdapterProvenance() { }

    private ObservationAdapterProvenance(
        Guid? credentialId,
        string adapterType,
        int adapterProtocolVersion,
        int configurationSchemaVersion,
        string sourceSystem,
        string? customerOwner)
    {
        this.CredentialId = credentialId;
        this.AdapterType = adapterType;
        this.AdapterProtocolVersion = adapterProtocolVersion;
        this.ConfigurationSchemaVersion = configurationSchemaVersion;
        this.SourceSystem = sourceSystem;
        this.CustomerOwner = customerOwner;
    }

    public Guid? CredentialId { get; private set; }
    public string AdapterType { get; private set; } = string.Empty;
    public int AdapterProtocolVersion { get; private set; }
    public int ConfigurationSchemaVersion { get; private set; }
    public string SourceSystem { get; private set; } = string.Empty;
    public string? CustomerOwner { get; private set; }

    public static Result<ObservationAdapterProvenance> Create(
        Guid? credentialId,
        string adapterType,
        int adapterProtocolVersion,
        int configurationSchemaVersion,
        string sourceSystem,
        string? customerOwner)
    {
        string normalizedAdapterType = adapterType?.Trim().ToLowerInvariant() ?? string.Empty;
        string normalizedSourceSystem = sourceSystem?.Trim().ToLowerInvariant() ?? string.Empty;
        string? normalizedOwner = string.IsNullOrWhiteSpace(customerOwner) ? null : customerOwner.Trim();
        if (credentialId == Guid.Empty ||
            !IsStableKey(normalizedAdapterType, AdapterProtocolLimits.AdapterTypeMaxLength) ||
            adapterProtocolVersion <= 0 ||
            configurationSchemaVersion <= 0 ||
            !IsStableKey(normalizedSourceSystem, AdapterIngressCredential.SourceSystemMaxLength) ||
            normalizedOwner?.Length > AdapterIngressCredential.ActorMaxLength ||
            (credentialId.HasValue && normalizedOwner is null))
        {
            return Result.Failure<ObservationAdapterProvenance>(
                IngestionDomainErrors.ReceiptProvenanceInvalid);
        }

        return Result.Success(new ObservationAdapterProvenance(
            credentialId,
            normalizedAdapterType,
            adapterProtocolVersion,
            configurationSchemaVersion,
            normalizedSourceSystem,
            normalizedOwner));
    }

    private static bool IsStableKey(string value, int maxLength) =>
        value.Length is > 0 &&
        value.Length <= maxLength &&
        char.IsAsciiLetterOrDigit(value[0]) &&
        value.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_');
}
