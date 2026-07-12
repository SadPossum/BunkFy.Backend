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
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<UpdatePropertyCommand, PropertyDto>
{
    public async Task<Result<PropertyDto>> HandleAsync(UpdatePropertyCommand command, CancellationToken cancellationToken)
    {
        Property? property = await repository.GetAsync(command.PropertyId, cancellationToken).ConfigureAwait(false);
        if (property is null)
        {
            return Result.Failure<PropertyDto>(PropertiesDomainErrors.PropertyNotFound);
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
            return Result.Failure<PropertyDto>(result.Error);
        }

        if (await repository.CodeExistsAsync(property.Code.Value, property.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<PropertyDto>(PropertiesDomainErrors.PropertyCodeAlreadyExists);
        }

        return Result.Success(PropertiesMapper.ToDto(property));
    }
}
