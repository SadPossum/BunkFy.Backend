namespace BunkFy.Modules.Workspaces.Domain.DataRights;

using Gma.Framework.Results;

public static class WorkspaceStaffCorrelationAnonymisationErrors
{
    public static readonly Error ReceiptInvalid = new(
        "Workspaces.StaffCorrelationAnonymisationReceiptInvalid",
        "The workspace Staff correlation anonymisation receipt is invalid.");

    public static readonly Error TombstoneInvalid = new(
        "Workspaces.StaffCorrelationAnonymisationTombstoneInvalid",
        "The workspace Staff correlation anonymisation tombstone is invalid.");

    public static readonly Error RestoreReceiptInvalid = new(
        "Workspaces.StaffCorrelationAnonymisationRestoreReceiptInvalid",
        "The workspace Staff correlation anonymisation restore receipt is invalid.");
}
