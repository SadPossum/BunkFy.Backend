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

internal sealed class UpdatePropertyCommandHandler(
    IPropertyRepository repository,
    PropertiesMutationCoordinator mutations,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<UpdatePropertyCommand, PropertyMutationReceiptDto>
{
    public async Task<Result<PropertyMutationReceiptDto>> HandleAsync(
        UpdatePropertyCommand command,
        CancellationToken cancellationToken)
    {
        Property? property = await mutations
            .AcquirePropertyAsync(
                command.PropertyId,
                cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<PropertyMutationReceiptDto>(PropertiesDomainErrors.PropertyNotFound);
        }

        Result result = property.Update(
            command.Name,
            command.Code,
            command.TimeZoneId,
            command.ExpectedVersion,
            idGenerator.NewId(),
            clock.UtcNow);
        if (result.IsFailure)
        {
            return Result.Failure<PropertyMutationReceiptDto>(result.Error);
        }

        await mutations.AcquirePropertyCodeAsync(
            property.Code,
            cancellationToken).ConfigureAwait(false);
        if (await repository.CodeExistsAsync(property.Code.Value, property.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PropertyMutationReceiptDto>(PropertiesDomainErrors.PropertyCodeAlreadyExists);
        }

        return Result.Success(PropertiesMapper.ToReceipt(property));
    }
}
