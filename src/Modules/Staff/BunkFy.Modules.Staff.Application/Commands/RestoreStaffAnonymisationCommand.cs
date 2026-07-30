namespace BunkFy.Modules.Staff.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Cqrs;

internal sealed record RestoreStaffAnonymisationCommand(
    DataRightsAnonymisationRestoreRequestV3 Request)
    : ITransactionalCommand<StaffAnonymisationRestoreReceipt>;
