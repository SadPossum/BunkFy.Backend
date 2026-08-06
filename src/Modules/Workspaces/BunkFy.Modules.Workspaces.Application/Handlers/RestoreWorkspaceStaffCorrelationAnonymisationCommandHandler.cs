namespace BunkFy.Modules.Workspaces.Application.Handlers;

using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Scoping;

internal sealed class
    RestoreWorkspaceStaffCorrelationAnonymisationCommandHandler(
        IWorkspaceStaffCorrelationAnonymisationRepository
            correlations,
        IWorkspaceCrossGraphMutationLock crossGraphLock,
        WorkspaceStaffAccessMutationCoordinator mutations,
        IWorkspaceStaffCorrelationOperationLock operationLock,
        IScopeContext scopeContext,
        ISystemClock clock)
    : ICommandHandler<
        RestoreWorkspaceStaffCorrelationAnonymisationCommand,
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
{
    public async Task<Result<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt>>
        HandleAsync(
            RestoreWorkspaceStaffCorrelationAnonymisationCommand
                command,
            CancellationToken cancellationToken)
    {
        string? tenantId =
            scopeContext.IsEnabled ? scopeContext.ScopeId : null;
        DataRightsAnonymisationRestoreRequestV3? request =
            command.Request;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Failure(
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .TenantRequired);
        }

        if (!IsValid(request, tenantId))
        {
            return Failure(
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .RestoreRequestInvalid);
        }

        await crossGraphLock.AcquireAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!await mutations.TryAcquireExistingCoordinateAsync(
                request.RecordId,
                cancellationToken).ConfigureAwait(false))
        {
            return Failure(
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .RestoreProofConflict);
        }

        bool locked = await operationLock.TryAcquireAsync(
            request.RecordId,
            cancellationToken).ConfigureAwait(false);
        if (!locked)
        {
            return Failure(
                WorkspaceStaffCorrelationAnonymisationApplicationErrors
                    .RestoreProofConflict);
        }

        return await correlations.RestoreAsync(
            new(
                tenantId,
                request.LedgerEntryId,
                request.TenantSequence,
                request.LedgerEntrySha256,
                request.RecordId,
                request.OwnerReceiptContractVersion,
                request.OwnerReceiptId,
                request.OwnerReceiptSha256,
                request.ResultingRecordVersion,
                request.OriginallyCompletedAtUtc.ToUniversalTime(),
                ToPersistencePrecision(clock.UtcNow)),
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsValid(
        DataRightsAnonymisationRestoreRequestV3? request,
        string tenantId) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsAnonymisationRestoreContractV3.CurrentVersion &&
        request.CaseType == DataRightsCaseType.StaffRights &&
        request.ScopeKind == DataRightsExecutionScopeKind.Tenant &&
        request.RoutingPropertyId is null &&
        string.Equals(
            request.TenantId,
            tenantId,
            StringComparison.Ordinal) &&
        request.LedgerEntryId != Guid.Empty &&
        request.TenantSequence > 0 &&
        IsSha256(request.LedgerEntrySha256) &&
        string.Equals(
            request.OwnerKey,
            WorkspacesDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.RecordType,
            WorkspacesDataRightsCoordinates
                .StaffAccessProcessRecordType,
            StringComparison.Ordinal) &&
        request.RecordId != Guid.Empty &&
        request.OwnerReceiptContractVersion > 0 &&
        request.OwnerReceiptId != Guid.Empty &&
        IsSha256(request.OwnerReceiptSha256) &&
        request.ResultingRecordVersion > 1 &&
        request.OriginallyCompletedAtUtc != default;

    private static bool IsSha256(string? value) =>
        value is
        {
            Length: DataRightsAnonymisationContract.Sha256Length
        } &&
        value.All(character =>
            character is (>= '0' and <= '9') or
                (>= 'a' and <= 'f'));

    private static DateTimeOffset ToPersistencePrecision(
        DateTimeOffset value)
    {
        const long ticksPerMicrosecond =
            TimeSpan.TicksPerMillisecond / 1000;
        return new(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            value.Offset);
    }

    private static Result<
        WorkspaceStaffCorrelationAnonymisationRestoreReceipt>
        Failure(Error error) =>
        Result.Failure<
            WorkspaceStaffCorrelationAnonymisationRestoreReceipt>(
            error);
}
