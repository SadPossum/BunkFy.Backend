namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Application.Queries;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class GetPropertyQueryHandler(IPropertiesReadRepository repository)
    : IQueryHandler<GetPropertyQuery, PropertyDto>
{
    public async Task<Result<PropertyDto>> HandleAsync(GetPropertyQuery query, CancellationToken cancellationToken)
    {
        PropertyDto? property = await repository.GetPropertyAsync(query.PropertyId, cancellationToken).ConfigureAwait(false);
        return property is null
            ? Result.Failure<PropertyDto>(PropertiesDomainErrors.PropertyNotFound)
            : Result.Success(property);
    }
}
