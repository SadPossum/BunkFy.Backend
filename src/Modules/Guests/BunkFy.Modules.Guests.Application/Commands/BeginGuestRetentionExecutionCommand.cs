namespace BunkFy.Modules.Guests.Application.Commands;

using BunkFy.Modules.Retention.Contracts;
using Gma.Framework.Cqrs;

internal sealed record BeginGuestRetentionExecutionCommand(
    RetentionContributionRequest Request)
    : ITransactionalCommand<GuestRetentionExecutionStart>;
