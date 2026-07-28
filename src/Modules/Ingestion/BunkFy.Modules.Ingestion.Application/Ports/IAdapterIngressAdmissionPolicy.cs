namespace BunkFy.Modules.Ingestion.Application.Ports;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Results;

public interface IAdapterIngressAdmissionPolicy
{
    Result Validate(IReadOnlyCollection<AdapterIngressObservationRequest> records);
}
