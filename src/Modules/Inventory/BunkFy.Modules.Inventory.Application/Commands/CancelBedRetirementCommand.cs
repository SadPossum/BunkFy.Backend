namespace BunkFy.Modules.Inventory.Application.Commands;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record CancelBedRetirementCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid TopologyChangeId,
    long ExpectedVersion,
    bool Confirmed,
    string Reason,
    string CanceledBy)
    : ITransactionalCommand<BedRetirementDto>;
