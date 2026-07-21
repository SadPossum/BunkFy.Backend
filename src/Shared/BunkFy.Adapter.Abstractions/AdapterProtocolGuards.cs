namespace BunkFy.Adapter.Abstractions;

internal static class AdapterProtocolGuards
{
    public static string Required(string? value, int maxLength, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} must contain between 1 and {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    public static string? Optional(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
        }

        return normalized;
    }

    public static string StableKey(string? value, int maxLength, string parameterName)
    {
        string normalized = Required(value, maxLength, parameterName).ToLowerInvariant();
        if (!char.IsAsciiLetterOrDigit(normalized[0]) || normalized.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new ArgumentException(
                $"{parameterName} must start with a letter or digit and contain only letters, digits, '.', '-', or '_'.",
                parameterName);
        }

        return normalized;
    }

    public static Guid Required(Guid value, string parameterName) =>
        value != Guid.Empty ? value : throw new ArgumentException($"{parameterName} is required.", parameterName);
}
