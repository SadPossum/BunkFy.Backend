namespace Properties.Contracts;

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
}
