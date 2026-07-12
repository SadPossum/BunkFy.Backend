namespace BunkFy.Modules.Ingestion.Application.Adapters;

using BunkFy.Adapter.Abstractions;

internal sealed class AdapterDescriptorRegistry : IAdapterDescriptorRegistry
{
    private readonly IReadOnlyDictionary<string, AdapterDescriptor> descriptors;

    public AdapterDescriptorRegistry(IEnumerable<IAdapterDescriptorProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        AdapterDescriptor[] registered = providers.Select(provider => provider.Descriptor).ToArray();
        string[] duplicates = registered
            .GroupBy(descriptor => descriptor.AdapterType, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Multiple adapter descriptors are registered for: {string.Join(", ", duplicates)}.");
        }

        this.descriptors = registered.ToDictionary(descriptor => descriptor.AdapterType, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<AdapterDescriptor> GetAll() =>
        this.descriptors.Values.OrderBy(descriptor => descriptor.AdapterType, StringComparer.Ordinal).ToArray();

    public bool TryGet(string adapterType, out AdapterDescriptor? descriptor) =>
        this.descriptors.TryGetValue(adapterType?.Trim().ToLowerInvariant() ?? string.Empty, out descriptor);
}
