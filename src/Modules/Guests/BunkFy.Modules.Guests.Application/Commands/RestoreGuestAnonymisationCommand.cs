namespace BunkFy.Modules.Guests.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Domain.DataRights;
using Gma.Framework.Cqrs;

internal sealed record RestoreGuestAnonymisationCommand(
    DataRightsAnonymisationRestoreRequest Request)
    : ITransactionalCommand<GuestAnonymisationRestoreReceipt>,
        IGuestsPersistenceRetryableCommand;
