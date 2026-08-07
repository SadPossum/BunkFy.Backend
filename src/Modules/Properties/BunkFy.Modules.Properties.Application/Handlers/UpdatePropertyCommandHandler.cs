namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;

internal sealed class UpdatePropertyCommandHandler(
    PropertiesMutationCoordinator mutations,
    PropertyDetailsUpdateCoordinator updates)
    : ICommandHandler<UpdatePropertyCommand, PropertyMutationReceiptDto>
{
    public async Task<Result<PropertyMutationReceiptDto>> HandleAsync(
        UpdatePropertyCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OperationId == Guid.Empty)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                PropertiesApplicationErrors.ManagementOperationInvalid);
        }

        Result<PropertyDetails> details = PropertyDetails.Create(
            command.Name,
            command.Code,
            command.TimeZoneId);
        if (details.IsFailure)
        {
            return Result.Failure<PropertyMutationReceiptDto>(
                details.Error);
        }

        Property? property = await mutations
            .AcquirePropertyAsync(
                command.PropertyId,
                cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<PropertyMutationReceiptDto>(PropertiesDomainErrors.PropertyNotFound);
        }

        return await updates.ExecuteAsync(
            property,
            command.OperationId,
            command.ExpectedVersion,
            details.Value,
            cancellationToken).ConfigureAwait(false);
    }
}
