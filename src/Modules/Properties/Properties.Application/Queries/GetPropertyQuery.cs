namespace Properties.Application.Queries;

using Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetPropertyQuery(Guid PropertyId) : IQuery<PropertyDto>;
