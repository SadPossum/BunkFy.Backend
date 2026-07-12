namespace BunkFy.Modules.Ingestion.Application.Adapters;

using BunkFy.Adapter.Abstractions;

public interface IAdapterDescriptorRegistry
{
    IReadOnlyCollection<AdapterDescriptor> GetAll();
    bool TryGet(string adapterType, out AdapterDescriptor? descriptor);
}
