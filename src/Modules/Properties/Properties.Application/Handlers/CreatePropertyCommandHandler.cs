namespace Properties.Application.Handlers;

using Properties.Application.Commands;
using Properties.Application.Mapping;
using Properties.Application.Ports;
using Properties.Contracts;
using Properties.Domain.Aggregates;
using Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class CreatePropertyCommandHandler(
    IPropertyRepository repository,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreatePropertyCommand, PropertyDto>
{
    public async Task<Result<PropertyDto>> HandleAsync(CreatePropertyCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<PropertyDto>(PropertiesDomainErrors.TenantRequired);
        }

        Result<Property> propertyResult = Property.Create(
            idGenerator.NewId(),
            scopeContext.ScopeId,
            command.Name,
            command.Code,
            command.TimeZoneId,
            idGenerator.NewId(),
            clock.UtcNow);

        if (propertyResult.IsFailure)
        {
            return Result.Failure<PropertyDto>(propertyResult.Error);
        }

        Property property = propertyResult.Value;
        if (await repository.CodeExistsAsync(property.Code.Value, excludingPropertyId: null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PropertyDto>(PropertiesDomainErrors.PropertyCodeAlreadyExists);
        }

        await repository.AddAsync(property, cancellationToken).ConfigureAwait(false);

        return Result.Success(PropertiesMapper.ToDto(property));
    }
}
