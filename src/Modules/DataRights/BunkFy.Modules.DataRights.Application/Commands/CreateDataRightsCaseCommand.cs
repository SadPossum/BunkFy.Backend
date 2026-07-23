namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record CreateDataRightsCaseCommand(
    Guid PropertyId,
    DataRightsOperation RequestedOperations,
    DataRightsRequesterRelationship RequesterRelationship,
    string ActorId) : ITransactionalCommand<DataRightsCaseDto>;
