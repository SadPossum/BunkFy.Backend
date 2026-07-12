namespace BunkFy.Adapters.JsonFileDrop;

using BunkFy.Adapter.Abstractions;

public sealed class JsonFileDropAdapterDescriptor : IAdapterDescriptorProvider
{
    public const string AdapterType = "json.file-drop";

    public static AdapterDescriptor Value { get; } = new(
        AdapterType,
        protocolVersion: 1,
        configurationSchemaVersion: 1,
        [AdapterExecutionMode.Polling, AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling],
        new AdapterPollingCapability(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)));

    public AdapterDescriptor Descriptor => Value;
}
