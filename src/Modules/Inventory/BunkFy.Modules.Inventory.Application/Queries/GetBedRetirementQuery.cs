namespace BunkFy.Modules.Inventory.Application.Queries;

using BunkFy.Modules.Inventory.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetBedRetirementQuery(Guid PropertyId, Guid TopologyChangeId)
    : IQuery<BedRetirementDto>;
