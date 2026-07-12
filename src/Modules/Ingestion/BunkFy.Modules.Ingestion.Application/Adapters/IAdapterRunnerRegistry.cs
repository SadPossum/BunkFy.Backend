namespace BunkFy.Modules.Ingestion.Application.Adapters;

using BunkFy.Adapter.Abstractions;

public interface IAdapterRunnerRegistry
{
    bool TryGet(string adapterType, out IAdapterRunner? runner);
}
