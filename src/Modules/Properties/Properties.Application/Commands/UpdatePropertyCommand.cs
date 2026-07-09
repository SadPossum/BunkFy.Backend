namespace Properties.Application.Commands;

using Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record UpdatePropertyCommand(
    Guid PropertyId,
    string Name,
    string Code,
    string TimeZoneId)
    : ITransactionalCommand<PropertyDto>;
