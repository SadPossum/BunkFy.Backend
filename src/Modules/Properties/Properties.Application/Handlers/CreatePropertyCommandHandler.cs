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
using Gma.Framework.Tenancy;

internal sealed class CreatePropertyCommandHandler(
    IPropertyRepository repository,
    ITenantContext tenantContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreatePropertyCommand, PropertyDto>
{
    public async Task<Result<PropertyDto>> HandleAsync(CreatePropertyCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantContext.TenantId))
        {
            return Result.Failure<PropertyDto>(PropertiesDomainErrors.TenantRequired);
        }

        Result<Property> propertyResult = Property.Create(
            idGenerator.NewId(),
            tenantContext.TenantId,
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
