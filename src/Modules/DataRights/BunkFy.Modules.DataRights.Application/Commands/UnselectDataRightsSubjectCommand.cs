namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record UnselectDataRightsSubjectCommand(
    Guid PropertyId,
    Guid CaseId,
    DataRightsSubjectCoordinateKey Coordinate,
    long ExpectedVersion,
    string ActorId) : ITransactionalCommand<DataRightsCaseDto>;
