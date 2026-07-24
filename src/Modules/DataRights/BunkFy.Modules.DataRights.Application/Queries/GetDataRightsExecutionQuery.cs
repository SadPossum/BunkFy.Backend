namespace BunkFy.Modules.DataRights.Application.Queries;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetDataRightsExecutionQuery(
    Guid PropertyId,
    Guid CaseId) : IQuery<DataRightsExecutionDto>;
