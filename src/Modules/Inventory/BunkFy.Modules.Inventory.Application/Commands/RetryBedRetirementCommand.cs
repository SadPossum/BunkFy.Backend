namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record RetryBedRetirementCommand(Guid PropertyId, Guid TopologyChangeId)
    : ITransactionalCommand<BedRetirementDto>;
