namespace BunkFy.Modules.Ingestion.Application.Adapters;

using BunkFy.Adapter.Abstractions;

public interface IAdapterObservationSinkFactory
{
    IAdapterObservationSink Create(AdapterRunAssignment assignment);
}
