namespace BunkFy.Modules.Workspaces.Application;

using Gma.Framework.Results;

internal static class WorkspaceTerminationApplicationErrors
{
    public static readonly Error ScopeRequired = new(
        "Workspaces.TerminationFenceScopeRequired",
        "A workspace scope is required for termination fence work.");
    public static readonly Error RequestInvalid = new(
        "Workspaces.TerminationFenceRequestInvalid",
        "The workspace termination fence request is invalid.");
    public static readonly Error ActiveFenceConflict = new(
        "Workspaces.TerminationFenceActiveConflict",
        "Another workspace termination fence is active.");
    public static readonly Error FenceNotFound = new(
        "Workspaces.TerminationFenceNotFound",
        "The workspace termination fence was not found.");
    public static readonly Error FenceCoordinatesConflict = new(
        "Workspaces.TerminationFenceCoordinatesConflict",
        "The workspace termination fence coordinates do not match.");
    public static readonly Error IdempotencyConflict = new(
        "Workspaces.TerminationFenceIdempotencyConflict",
        "The workspace termination fence idempotency key was reused.");
    public static readonly Error OwnerProofInvalid = new(
        "Workspaces.TerminationFenceOwnerProofInvalid",
        "The workspace termination fence owner proof is invalid.");
}
