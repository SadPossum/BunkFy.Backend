namespace Properties.Application.Handlers;

using Properties.Application.Ports;
using Properties.Application.Queries;
using Properties.Contracts;
using Properties.Domain.Errors;
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
