namespace BunkFy.Modules.Properties.Application.Handlers;

using System.Globalization;
using BunkFy.Modules.Properties.Domain.ValueObjects;

internal static class PropertyDetailsUpdateFingerprint
{
    public static string Compute(
        Guid propertyId,
        long expectedVersion,
        PropertyDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return PropertiesMutationFingerprint.Compute(
            "bunkfy-properties-details-update/v1",
            propertyId.ToString("N"),
            expectedVersion.ToString(CultureInfo.InvariantCulture),
            details.Name.Value,
            details.Code.Value,
            details.TimeZoneId.Value);
    }
}
