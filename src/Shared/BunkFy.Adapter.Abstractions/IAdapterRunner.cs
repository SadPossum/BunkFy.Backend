namespace BunkFy.Adapter.Abstractions;

public interface IAdapterRunner
{
    AdapterDescriptor Descriptor { get; }

    Task<AdapterRunCompletion> RunAsync(
        AdapterRunAssignment assignment,
        AdapterConfigurationMaterial material,
        IAdapterObservationSink sink,
        CancellationToken cancellationToken);
}
