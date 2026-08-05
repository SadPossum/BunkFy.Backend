namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record RequestTenantTerminationCommand(
    Guid RequestId,
    bool ExportRequested,
    DataRightsRequesterRelationship RequesterRelationship,
    string ActorId) : ITransactionalCommand<TenantTerminationCaseDto>;
