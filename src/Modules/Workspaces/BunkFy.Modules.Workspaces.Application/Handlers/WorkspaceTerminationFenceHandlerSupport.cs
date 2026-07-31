namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Domain.Termination;
using Gma.Framework.Results;

internal static class WorkspaceTerminationFenceHandlerSupport
{
    public static bool HasValidCoordinates(
        Guid idempotencyKey,
        Guid processId,
        Guid caseId,
        long approvalRevision,
        long operationRevision,
        Guid workItemId,
        Guid terminationEpoch,
        string? policyEvidenceSha256,
        string? actorId) =>
        idempotencyKey != Guid.Empty &&
        processId != Guid.Empty &&
        caseId != Guid.Empty &&
        approvalRevision > 0 &&
        operationRevision > 0 &&
        workItemId != Guid.Empty &&
        terminationEpoch != Guid.Empty &&
        IsSha256(policyEvidenceSha256) &&
        IsActor(actorId);

    public static bool MatchesFence(
        WorkspaceTerminationFence fence,
        Guid processId,
        Guid caseId,
        long approvalRevision,
        Guid terminationEpoch,
        string policyEvidenceSha256) =>
        fence.ProcessId == processId &&
        fence.CaseId == caseId &&
        fence.ApprovalRevision == approvalRevision &&
        fence.TerminationEpoch == terminationEpoch &&
        string.Equals(
            fence.PolicyEvidenceSha256,
            policyEvidenceSha256,
            StringComparison.Ordinal);

    public static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }

    public static Result<T> Failure<T>(Error error) =>
        Result.Failure<T>(error);

    private static bool IsActor(string? actorId)
    {
        string normalized = actorId?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= 200;
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
