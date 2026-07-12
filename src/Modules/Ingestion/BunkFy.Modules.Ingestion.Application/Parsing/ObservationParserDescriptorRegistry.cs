namespace BunkFy.Modules.Ingestion.Application.Parsing;

using BunkFy.ObservationParsing;

internal sealed class ObservationParserDescriptorRegistry : IObservationParserDescriptorRegistry
{
    private readonly Dictionary<(string Type, int Version), ObservationParserDescriptor> descriptors;

    public ObservationParserDescriptorRegistry(IEnumerable<IObservationParserDescriptorProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ObservationParserDescriptor[] registered = providers.Select(provider => provider.Descriptor).ToArray();
        (string, int)[] duplicates = registered
            .GroupBy(descriptor => (descriptor.ParserType, descriptor.ParserVersion))
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Multiple parser descriptors are registered for: {string.Join(", ", duplicates)}.");
        }

        this.descriptors = registered.ToDictionary(
            descriptor => (descriptor.ParserType, descriptor.ParserVersion));
    }

    public IReadOnlyCollection<ObservationParserDescriptor> GetAll() => this.descriptors.Values
        .OrderBy(descriptor => descriptor.ParserType, StringComparer.Ordinal)
        .ThenByDescending(descriptor => descriptor.ParserVersion)
        .ToArray();

    public bool TryGet(string parserType, int? parserVersion, out ObservationParserDescriptor? descriptor)
    {
        string normalized = parserType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (parserVersion.HasValue)
        {
            return this.descriptors.TryGetValue((normalized, parserVersion.Value), out descriptor);
        }

        descriptor = this.descriptors.Values
            .Where(candidate => string.Equals(candidate.ParserType, normalized, StringComparison.Ordinal))
            .OrderByDescending(candidate => candidate.ParserVersion)
            .FirstOrDefault();
        return descriptor is not null;
    }
}
