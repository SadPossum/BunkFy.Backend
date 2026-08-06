namespace BunkFy.Modules.Properties.Application.Handlers;

using BunkFy.Modules.Properties.Application.Commands;
using BunkFy.Modules.Properties.Application.Mapping;
using BunkFy.Modules.Properties.Application.Ports;
using BunkFy.Modules.Properties.Contracts;
using BunkFy.Modules.Properties.Domain.Aggregates;
using BunkFy.Modules.Properties.Domain.Entities;
using BunkFy.Modules.Properties.Domain.Errors;
using BunkFy.Modules.Properties.Domain.ValueObjects;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Identity;
using Gma.Framework.Runtime.Time;

internal sealed class AddBedsCommandHandler(
    PropertiesMutationCoordinator mutations,
    ISystemClock clock,
    IIdGenerator idGenerator)
    : ICommandHandler<AddBedsCommand, BedBatchMutationReceiptDto>
{
    public async Task<Result<BedBatchMutationReceiptDto>> HandleAsync(
        AddBedsCommand command,
        CancellationToken cancellationToken)
    {
        if (command.Labels is null || command.Labels.Count == 0)
        {
            return Result.Failure<BedBatchMutationReceiptDto>(PropertiesApplicationErrors.BedBatchRequired);
        }

        if (command.Labels.Count > PropertiesContractLimits.MaximumBedsPerBatch)
        {
            return Result.Failure<BedBatchMutationReceiptDto>(PropertiesApplicationErrors.BedBatchTooLarge);
        }

        Room? room = await mutations
            .AcquireRoomAsync(
                command.RoomId,
                cancellationToken).ConfigureAwait(false);
        if (room is null || room.PropertyId != command.PropertyId)
        {
            return Result.Failure<BedBatchMutationReceiptDto>(PropertiesDomainErrors.RoomNotFound);
        }

        BedAdditionDefinition[] additions = command.Labels
            .Select(label => new BedAdditionDefinition(idGenerator.NewId(), label, idGenerator.NewId()))
            .ToArray();
        Result<IReadOnlyCollection<Bed>> result = room.AddBeds(
            additions,
            command.ExpectedRoomVersion,
            clock.UtcNow);
        return result.IsSuccess
            ? Result.Success(PropertiesMapper.ToBatchReceipt(room, result.Value.Count))
            : Result.Failure<BedBatchMutationReceiptDto>(result.Error);
    }
}
