namespace BunkFy.Modules.Properties.Application.Commands;

using BunkFy.Modules.Properties.Contracts;
using Gma.Framework.Cqrs;

public sealed record AddBedsCommand(
    Guid OperationId,
    Guid PropertyId,
    Guid RoomId,
    long ExpectedRoomVersion,
    IReadOnlyCollection<string> Labels) : ITransactionalCommand<BedBatchMutationReceiptDto>;
