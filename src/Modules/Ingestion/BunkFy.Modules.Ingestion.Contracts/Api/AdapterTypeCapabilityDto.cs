namespace BunkFy.Modules.Ingestion.Contracts;

using BunkFy.Adapter.Abstractions;

public sealed record AdapterTypeCapabilityDto(
    string AdapterType,
    int ProtocolVersion,
    int ConfigurationSchemaVersion,
    IReadOnlyCollection<AdapterExecutionMode> ExecutionModes,
    long? MinimumPollingIntervalSeconds,
    long? RecommendedPollingIntervalSeconds);

public sealed record AdapterTypeCapabilityListResponse(
    IReadOnlyCollection<AdapterTypeCapabilityDto> AdapterTypes);
