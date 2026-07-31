namespace BunkFy.Modules.Workspaces.Domain.Termination;

using Gma.Framework.Results;

public static class WorkspaceTerminationFenceErrors
{
    public static readonly Error IdentityInvalid = new(
        "Workspaces.TerminationFenceIdentityInvalid",
        "The workspace termination fence identity is invalid.");
    public static readonly Error CoordinatesInvalid = new(
        "Workspaces.TerminationFenceCoordinatesInvalid",
        "The workspace termination fence coordinates are invalid.");
    public static readonly Error VersionConflict = new(
        "Workspaces.TerminationFenceVersionConflict",
        "The workspace termination fence version is no longer current.");
    public static readonly Error TransitionInvalid = new(
        "Workspaces.TerminationFenceTransitionInvalid",
        "The workspace termination fence transition is invalid.");
    public static readonly Error ReceiptIdentityInvalid = new(
        "Workspaces.TerminationFenceReceiptIdentityInvalid",
        "The workspace termination fence receipt identity is invalid.");
    public static readonly Error ReceiptCoordinatesInvalid = new(
        "Workspaces.TerminationFenceReceiptCoordinatesInvalid",
        "The workspace termination fence receipt coordinates are invalid.");
    public static readonly Error ReceiptTransitionInvalid = new(
        "Workspaces.TerminationFenceReceiptTransitionInvalid",
        "The workspace termination fence receipt transition is invalid.");
}
