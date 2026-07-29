namespace BunkFy.Modules.Guests.Application.Commands;

using Gma.Framework.Cqrs;

internal sealed record ApplyGuestRetentionCommand(
    Guid ExecutionId,
    Guid GuestId,
    long ExpectedGuestVersion)
    : ITransactionalCommand<GuestRetentionMutationResult>;
