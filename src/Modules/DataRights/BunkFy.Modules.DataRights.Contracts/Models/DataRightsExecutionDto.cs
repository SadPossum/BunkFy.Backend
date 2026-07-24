namespace BunkFy.Modules.DataRights.Contracts;

public sealed record DataRightsExecutionDto(
    DataRightsCaseDto Case,
    DataRightsExecutionWorkItemDto WorkItem);
