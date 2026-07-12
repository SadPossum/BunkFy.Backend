namespace BunkFy.ObservationParsing;

internal static class ObservationParserGuards
{
    public static string StableKey(string? value, int maxLength, string parameterName)
    {
        string normalized = Required(value, maxLength, parameterName).ToLowerInvariant();
        if (!char.IsLetterOrDigit(normalized[0]) || normalized.Any(character =>
                !char.IsLetterOrDigit(character) && character is not '.' and not '-' and not '_'))
        {
            throw new ArgumentException(
                $"{parameterName} must start with a letter or digit and contain only letters, digits, '.', '-', or '_'.",
                parameterName);
        }

        return normalized;
    }

    public static string Required(string? value, int maxLength, string parameterName)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length is 0 || normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{parameterName} must contain between 1 and {maxLength} characters.",
                parameterName);
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
        return normalized.Length <= maxLength
            ? normalized
            : throw new ArgumentException($"{parameterName} cannot exceed {maxLength} characters.", parameterName);
    }
}
