namespace BunkFy.Modules.Properties.Contracts;

using Gma.Framework.Messaging;

internal static class PropertiesEventContractGuards
{
    public static string? NormalizeOptionalLabel(string? value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : IntegrationEventContractGuards.NormalizeRequiredText(value, PropertiesContractLimits.PhysicalLabelMaxLength, parameterName);

    public static PropertyStatus RequireKnown(PropertyStatus status, string parameterName) =>
        status is PropertyStatus.Active or PropertyStatus.Retired
            ? status
            : throw new ArgumentOutOfRangeException(parameterName, status, "Property status is not supported.");

    public static RoomStatus RequireKnown(RoomStatus status, string parameterName) =>
        status is RoomStatus.Active or RoomStatus.Retired
            ? status
            : throw new ArgumentOutOfRangeException(parameterName, status, "Room status is not supported.");

    public static BedStatus RequireKnown(BedStatus status, string parameterName) =>
        status is BedStatus.Active or BedStatus.Retired
            ? status
            : throw new ArgumentOutOfRangeException(parameterName, status, "Bed status is not supported.");

    public static long RequireVersion(long version, string parameterName) =>
        version > 0
            ? version
            : throw new ArgumentOutOfRangeException(parameterName, version, "Entity version must be positive.");

    public static int RequireVersion(int version, string parameterName) =>
        version > 0
            ? version
            : throw new ArgumentOutOfRangeException(parameterName, version, "Entity version must be positive.");

    public static string RequireCountryCode(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == PropertiesContractLimits.CountryCodeLength &&
               normalized.All(character => character is >= 'A' and <= 'Z')
            ? normalized
            : throw new ArgumentException("An uppercase ISO 3166-1 alpha-2 country code is required.", parameterName);
    }

    public static string RequirePolicyKey(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= PropertiesContractLimits.PolicyKeyMaxLength &&
               normalized[0] is >= 'a' and <= 'z' &&
               normalized.All(character =>
                   character is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '-' or '_')
            ? normalized
            : throw new ArgumentException("A lowercase ASCII policy key is required.", parameterName);
    }

    public static string RequireSha256(string value, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == PropertiesContractLimits.ContentSha256Length &&
               normalized.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            ? normalized
            : throw new ArgumentException("A lowercase SHA-256 digest is required.", parameterName);
    }

    public static IReadOnlyCollection<PropertyGovernanceAcknowledgement> RequireAcknowledgements(
        IReadOnlyCollection<PropertyGovernanceAcknowledgement>? acknowledgements)
    {
        PropertyGovernanceAcknowledgement[] values = acknowledgements?.ToArray() ??
            throw new ArgumentNullException(nameof(acknowledgements));
        if (values.Length > PropertiesContractLimits.MaximumPolicyAcknowledgements ||
            values.Distinct().Count() != values.Length)
        {
            throw new ArgumentException("A bounded unique acknowledgement set is required.", nameof(acknowledgements));
        }

        return values;
    }
}
