namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsExecutionDto(
    DataRightsCaseDto Case,
    DataRightsExecutionBatchDto Batch,
    IReadOnlyCollection<DataRightsExecutionWorkItemDto> WorkItems);
