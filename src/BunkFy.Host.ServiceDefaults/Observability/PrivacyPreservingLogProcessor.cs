namespace BunkFy.Host.ServiceDefaults.Observability;

using System.Text.RegularExpressions;
using OpenTelemetry;
using OpenTelemetry.Logs;

internal sealed class PrivacyPreservingLogProcessor : BaseProcessor<LogRecord>
{
    private const int MaximumDimensionLength = 128;
    private const string OriginalFormatAttribute = "{OriginalFormat}";
    private const string UnstructuredLogBody = "Unstructured application log event";

    private static readonly Regex SafeDimensionPattern = new(
        "^[A-Za-z0-9._:+/-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public override void OnEnd(LogRecord data)
    {
        ArgumentNullException.ThrowIfNull(data);

        IReadOnlyList<KeyValuePair<string, object?>> attributes = data.Attributes ?? [];
        string? originalFormat = attributes
            .FirstOrDefault(attribute => string.Equals(
                attribute.Key,
                OriginalFormatAttribute,
                StringComparison.Ordinal))
            .Value as string;
        List<KeyValuePair<string, object?>> safeAttributes = attributes
            .Where(attribute => IsSafeAttribute(attribute, originalFormat))
            .ToList();

        data.FormattedMessage = null;
        data.Exception = null;
        data.Body = originalFormat ?? UnstructuredLogBody;
        data.Attributes = safeAttributes;
    }

    private static bool IsSafeAttribute(
        KeyValuePair<string, object?> attribute,
        string? originalFormat)
    {
        if (string.Equals(attribute.Key, OriginalFormatAttribute, StringComparison.Ordinal))
        {
            return originalFormat is not null;
        }

        if (IsSensitivePropertyName(attribute.Key))
        {
            return false;
        }

        bool safeScalar = attribute.Value is bool
            or byte
            or sbyte
            or short
            or ushort
            or int
            or uint
            or long
            or ulong
            or float
            or double
            or decimal
            or Enum;
        bool safeString = attribute.Value is string dimension
                          && dimension.Length is > 0 and <= MaximumDimensionLength
                          && SafeDimensionPattern.IsMatch(dimension);

        return safeScalar || safeString;
    }

    private static bool IsSensitivePropertyName(string name) =>
        (!string.Equals(name, "TraceId", StringComparison.OrdinalIgnoreCase)
         && name.EndsWith("Id", StringComparison.OrdinalIgnoreCase))
        || name.Contains("Tenant", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Scope", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("User", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Actor", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Identity", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Payload", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Body", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Token", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Email", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Phone", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Reason", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Error", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Message", StringComparison.OrdinalIgnoreCase);
}
