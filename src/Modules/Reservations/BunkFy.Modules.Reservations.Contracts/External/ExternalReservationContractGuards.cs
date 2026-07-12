namespace BunkFy.Modules.Reservations.Contracts;

using Gma.Framework.Messaging;

internal static class ExternalReservationContractGuards
{
    public static string Required(string value, int maximumLength, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 && normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"{parameterName} is required and too long.", parameterName);
    }

    public static string? Optional(string? value, int maximumLength, string parameterName)
    {
        string? normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return normalized is null || normalized.Length <= maximumLength
            ? normalized
            : throw new ArgumentException($"{parameterName} is too long.", parameterName);
    }

    public static Guid Id(Guid value, string parameterName) =>
        IntegrationEventContractGuards.RequireId(value, parameterName);

    public static void Common(Guid operationId, Guid receiptId, Guid connectionId, Guid propertyId)
    {
        _ = Id(operationId, nameof(operationId));
        _ = Id(receiptId, nameof(receiptId));
        _ = Id(connectionId, nameof(connectionId));
        _ = Id(propertyId, nameof(propertyId));
    }
}
