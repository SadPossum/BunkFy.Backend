namespace BunkFy.ObservationParsing;

using BunkFy.Adapter.Abstractions;

public sealed record ObservationParserDescriptor
{
    public ObservationParserDescriptor(
        string parserType,
        int parserVersion,
        IReadOnlyCollection<string> supportedAdapterTypes,
        IReadOnlyCollection<string> supportedSourceRecordTypes,
        IReadOnlyCollection<string> outputRecordTypes)
    {
        this.ParserType = ObservationParserGuards.StableKey(
            parserType,
            ObservationParserLimits.ParserTypeMaxLength,
            nameof(parserType));
        this.ParserVersion = parserVersion > 0
            ? parserVersion
            : throw new ArgumentOutOfRangeException(nameof(parserVersion));
        this.SupportedAdapterTypes = NormalizeKeys(
            supportedAdapterTypes,
            AdapterProtocolLimits.AdapterTypeMaxLength,
            nameof(supportedAdapterTypes));
        this.SupportedSourceRecordTypes = NormalizeKeys(
            supportedSourceRecordTypes,
            AdapterProtocolLimits.RecordTypeMaxLength,
            nameof(supportedSourceRecordTypes));
        this.OutputRecordTypes = NormalizeKeys(
            outputRecordTypes,
            AdapterProtocolLimits.RecordTypeMaxLength,
            nameof(outputRecordTypes));
    }

    public string ParserType { get; }
    public int ParserVersion { get; }
    public IReadOnlyCollection<string> SupportedAdapterTypes { get; }
    public IReadOnlyCollection<string> SupportedSourceRecordTypes { get; }
    public IReadOnlyCollection<string> OutputRecordTypes { get; }

    public bool Supports(string adapterType, string sourceRecordType) =>
        this.SupportedAdapterTypes.Contains(adapterType?.Trim().ToLowerInvariant() ?? string.Empty, StringComparer.Ordinal) &&
        this.SupportedSourceRecordTypes.Contains(sourceRecordType?.Trim().ToLowerInvariant() ?? string.Empty, StringComparer.Ordinal);

    private static string[] NormalizeKeys(
        IReadOnlyCollection<string> values,
        int maxLength,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);
        string[] normalized = values
            .Select(value => ObservationParserGuards.StableKey(value, maxLength, parameterName))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return normalized.Length > 0
            ? normalized
            : throw new ArgumentException("At least one value is required.", parameterName);
    }
}
