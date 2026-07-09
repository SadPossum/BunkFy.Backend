namespace Properties.Application.Commands;

using Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record CreatePropertyCommand(
    string Name,
    string Code,
    string TimeZoneId)
    : ITransactionalCommand<PropertyDto>;
