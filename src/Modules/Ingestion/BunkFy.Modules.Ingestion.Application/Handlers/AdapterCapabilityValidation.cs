namespace BunkFy.Modules.Ingestion.Application.Handlers;

using BunkFy.Adapter.Abstractions;
using Gma.Framework.Results;
using BunkFy.Modules.Ingestion.Application.Adapters;

internal static class AdapterCapabilityValidation
{
    public static Result Validate(
        IAdapterDescriptorRegistry descriptors,
        string adapterType,
        AdapterExecutionMode executionMode)
    {
        if (!descriptors.TryGet(adapterType, out AdapterDescriptor? descriptor) || descriptor is null)
        {
            return Result.Failure(IngestionApplicationErrors.AdapterTypeNotRegistered);
        }

        return descriptor.ExecutionModes.Contains(executionMode)
            ? Result.Success()
            : Result.Failure(IngestionApplicationErrors.AdapterExecutionModeUnsupported);
    }
}
