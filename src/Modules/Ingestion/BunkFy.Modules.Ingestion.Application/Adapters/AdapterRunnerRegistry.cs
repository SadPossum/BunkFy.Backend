namespace BunkFy.Modules.Ingestion.Application.Adapters;

using BunkFy.Adapter.Abstractions;

internal sealed class AdapterRunnerRegistry : IAdapterRunnerRegistry
{
    private readonly Dictionary<string, IAdapterRunner> runners;

    public AdapterRunnerRegistry(IEnumerable<IAdapterRunner> runners)
    {
        ArgumentNullException.ThrowIfNull(runners);
        IAdapterRunner[] registered = runners.ToArray();
        string[] duplicates = registered
            .GroupBy(runner => runner.Descriptor.AdapterType, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Multiple adapter runners are registered for: {string.Join(", ", duplicates)}.");
        }

        this.runners = registered.ToDictionary(
            runner => runner.Descriptor.AdapterType,
            StringComparer.Ordinal);
    }

    public bool TryGet(string adapterType, out IAdapterRunner? runner) =>
        this.runners.TryGetValue(adapterType?.Trim().ToLowerInvariant() ?? string.Empty, out runner);
}
