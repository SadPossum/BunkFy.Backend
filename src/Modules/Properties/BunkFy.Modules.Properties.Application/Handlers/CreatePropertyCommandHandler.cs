namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class CreatePropertyCommandHandler(
    IPropertyRepository repository,
    PropertiesMutationCoordinator mutations,
    IPropertiesCreationOperationLock creationLock,
    IScopeContext scopeContext,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<CreatePropertyCommand, PropertyMutationReceiptDto>
{
    public async Task<Result<PropertyMutationReceiptDto>> HandleAsync(
        CreatePropertyCommand command,
        CancellationToken cancellationToken)
    {
        if (!scopeContext.IsEnabled ||
            string.IsNullOrWhiteSpace(scopeContext.ScopeId))
        {
            return Result.Failure<PropertyMutationReceiptDto>(PropertiesDomainErrors.TenantRequired);
        }

        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                PropertiesApplicationErrors.CreationOperationInvalid);
        }

        Result<PropertyDetails> details = PropertyDetails.Create(
            command.Name,
            command.Code,
            command.TimeZoneId);
        if (details.IsFailure)
        {
            return Result.Failure<PropertyMutationReceiptDto>(details.Error);
        }

        await creationLock.AcquireAsync(
            scopeContext.ScopeId,
            command.OperationId,
            cancellationToken).ConfigureAwait(false);
        Property? existing = await repository.GetAsync(
            command.OperationId,
            cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return existing.MatchesCreation(details.Value)
                ? Result.Success(PropertiesMapper.ToReceipt(existing))
                : Result.Failure<PropertyMutationReceiptDto>(
                    PropertiesApplicationErrors.CreationOperationConflict);
        }

        await mutations.AcquirePropertyCodeAsync(
            details.Value.Code,
            cancellationToken).ConfigureAwait(false);
        if (await repository.CodeExistsAsync(
                details.Value.Code.Value,
                excludingPropertyId: null,
                cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PropertyMutationReceiptDto>(PropertiesDomainErrors.PropertyCodeAlreadyExists);
        }

        Result<Property> propertyResult = Property.Create(
            command.OperationId,
            scopeContext.ScopeId,
            details.Value,
            idGenerator.NewId(),
            clock.UtcNow);
        if (propertyResult.IsFailure)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                propertyResult.Error);
        }

        Property property = propertyResult.Value;
        await repository.AddAsync(property, cancellationToken).ConfigureAwait(false);

        return Result.Success(PropertiesMapper.ToReceipt(property));
    }
}
