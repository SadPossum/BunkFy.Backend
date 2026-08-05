namespace BunkFy.Modules.DataRights.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using Gma.Framework.Cqrs;

public sealed record RequestTenantTerminationCancellationCommand(
    Guid ProcessId,
    long ExpectedProcessVersion,
    string ActorId) : ITransactionalCommand<TenantTerminationProcessDto>;
