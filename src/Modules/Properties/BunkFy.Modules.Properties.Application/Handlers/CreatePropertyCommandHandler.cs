namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class CreatePropertyCommandHandler(
    IPropertyRepository repository,
    PropertiesMutationCoordinator mutations,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreatePropertyCommand, PropertyMutationReceiptDto>
{
    public async Task<Result<PropertyMutationReceiptDto>> HandleAsync(
        CreatePropertyCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<PropertyMutationReceiptDto>(PropertiesDomainErrors.TenantRequired);
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
            return Result.Failure<PropertyMutationReceiptDto>(propertyResult.Error);
        }

        Property property = propertyResult.Value;
        await mutations.AcquirePropertyCodeAsync(
            property.Code,
            cancellationToken).ConfigureAwait(false);
        if (await repository.CodeExistsAsync(property.Code.Value, excludingPropertyId: null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PropertyMutationReceiptDto>(PropertiesDomainErrors.PropertyCodeAlreadyExists);
        }

        await repository.AddAsync(property, cancellationToken).ConfigureAwait(false);

        return Result.Success(PropertiesMapper.ToReceipt(property));
    }
}
