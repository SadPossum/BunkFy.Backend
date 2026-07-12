namespace BunkFy.Modules.Ingestion.Application.Parsing;

using BunkFy.ObservationParsing;

internal sealed class ObservationParserRegistry : IObservationParserRegistry
{
    private readonly Dictionary<(string Type, int Version), IObservationParser> parsers;

    public ObservationParserRegistry(IEnumerable<IObservationParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);
        IObservationParser[] registered = parsers.ToArray();
        (string, int)[] duplicates = registered
            .GroupBy(parser => (parser.Descriptor.ParserType, parser.Descriptor.ParserVersion))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Multiple parser runners are registered for: {string.Join(", ", duplicates)}.");
        }

        this.parsers = registered.ToDictionary(
            parser => (parser.Descriptor.ParserType, parser.Descriptor.ParserVersion));
    }

    public bool TryGet(string parserType, int parserVersion, out IObservationParser? parser) =>
        this.parsers.TryGetValue((parserType?.Trim().ToLowerInvariant() ?? string.Empty, parserVersion), out parser);
}
