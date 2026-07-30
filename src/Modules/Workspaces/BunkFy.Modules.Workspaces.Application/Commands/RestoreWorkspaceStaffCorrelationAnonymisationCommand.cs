namespace BunkFy.Modules.Workspaces.Application.Commands;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Cqrs;

internal sealed record
    RestoreWorkspaceStaffCorrelationAnonymisationCommand(
        DataRightsAnonymisationRestoreRequestV3 Request)
    : ITransactionalCommand<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt>;
