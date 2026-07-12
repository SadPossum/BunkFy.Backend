namespace BunkFy.Adapters.FakeHttp;

using BunkFy.Adapter.Abstractions;

public sealed class FakeHttpAdapterDescriptor : IAdapterDescriptorProvider
{
    public static AdapterDescriptor Value { get; } = new(
        FakeHttpAdapterRunner.AdapterType,
        protocolVersion: 1,
        configurationSchemaVersion: 1,
        [AdapterExecutionMode.Polling, AdapterExecutionMode.Push, AdapterExecutionMode.RemotePolling],
        new AdapterPollingCapability(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5)));

    public AdapterDescriptor Descriptor => Value;
}
