namespace BunkFy.Modules.Properties.Application.Queries;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record GetPropertyProcessingStateQuery(Guid PropertyId)
    : IQuery<PropertyProcessingStateDto>;
