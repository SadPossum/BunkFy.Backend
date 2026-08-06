namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Domain;
using Gma.Framework.Cqrs;
using Gma.Framework.Naming;
using Gma.Framework.Results;
using Gma.Framework.Scoping;

internal sealed class
    ScrubWorkspaceStaffRetentionCorrelationCommandHandler(
    IWorkspaceStaffRetentionCorrelationRepository repository,
    WorkspaceStaffAccessMutationCoordinator mutations,
    IScopeContext scopeContext)
    : ICommandHandler<
        ScrubWorkspaceStaffRetentionCorrelationCommand,
        WorkspaceStaffRetentionCorrelationReceipt>
{
    public async Task<Result<WorkspaceStaffRetentionCorrelationReceipt>>
        HandleAsync(
            ScrubWorkspaceStaffRetentionCorrelationCommand command,
            CancellationToken cancellationToken)
    {
        string? subjectId = NormalizeSubject(command.SubjectId);
        if (!scopeContext.IsEnabled ||
            !TenantIds.TryNormalize(
                scopeContext.ScopeId,
                out string? activeScopeId) ||
            !TenantIds.TryNormalize(
                command.TenantId,
                out string? tenantId) ||
            !string.Equals(
                activeScopeId,
                tenantId,
                StringComparison.Ordinal) ||
            command.ReceiptId == Guid.Empty ||
            command.ExecutionId == Guid.Empty ||
            command.StaffMemberId == Guid.Empty ||
            command.SelectedStaffVersion <= 0 ||
            command.CompletedAtUtc == default ||
            subjectId?.Length >
                WorkspaceStaffAccessProcess.SubjectIdMaxLength)
        {
            return Result.Failure<
                WorkspaceStaffRetentionCorrelationReceipt>(
                WorkspaceStaffRetentionErrors.RequestInvalid);
        }

        await mutations.AcquireStaffAsync(
                command.StaffMemberId,
                cancellationToken).ConfigureAwait(false);
        return await repository.ScrubAsync(
            new WorkspaceStaffRetentionCorrelationScrubRequest(
                command.ReceiptId,
                command.ExecutionId,
                tenantId,
                command.StaffMemberId,
                command.SelectedStaffVersion,
                subjectId,
                ToPersistencePrecision(
                    command.CompletedAtUtc.ToUniversalTime())),
            cancellationToken).ConfigureAwait(false);
    }

    private static string? NormalizeSubject(string? subjectId)
    {
        string value = subjectId?.Trim() ?? string.Empty;
        return value.Length == 0 ? null : value;
    }

    private static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }
}
