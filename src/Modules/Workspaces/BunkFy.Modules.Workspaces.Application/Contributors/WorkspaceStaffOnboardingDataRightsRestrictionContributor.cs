namespace BunkFy.Modules.Workspaces.Application.Contributors;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Workspaces.Application.Commands;
using BunkFy.Modules.Workspaces.Application.Mapping;
using BunkFy.Modules.Workspaces.Application.Ports;
using BunkFy.Modules.Workspaces.Contracts;
using BunkFy.Modules.Workspaces.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class
    WorkspaceStaffOnboardingDataRightsRestrictionContributor(
        IRequestDispatcher dispatcher,
        IWorkspaceStaffOnboardingProcessingRestrictionProjectionRepository
            projections,
        IWorkspaceStaffOnboardingProcessingRestrictionRepository restrictions,
        ISystemClock clock)
    : IDataRightsRestrictionContributor
{
    private static readonly PageRequest ActiveRestrictionPage = new(1, 2);

    public string OwnerKey => WorkspacesDataRightsCoordinates.Owner;

    public int ContractVersion =>
        DataRightsRestrictionContract.CurrentVersion;

    public async Task<DataRightsRestrictionContributionResult> ExecuteAsync(
        DataRightsRestrictionContributionRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValid(request, clock.UtcNow))
        {
            return DataRightsRestrictionContributionResult.Failed(
                WorkspaceStaffOnboardingApplicationErrors
                    .RestrictionRequestInvalid.Code);
        }

        WorkspaceStaffOnboardingProcessingRestrictionReceipt? replay =
            await restrictions.FindReceiptByIdempotencyKeyAsync(
                request.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return ToResult(request, replay.ToDto());
        }

        WorkspaceStaffOnboardingProcessingRestrictionProjection? projection =
            await projections.GetAsync(
                request.Coordinate.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (projection is null ||
            projection.ContractVersion !=
                WorkspaceStaffOnboardingProcessingRestrictionContract
                    .CurrentVersion)
        {
            return DataRightsRestrictionContributionResult.Blocked(
                WorkspaceStaffOnboardingApplicationErrors
                    .RestrictionProjectionUnavailable.Code);
        }

        IReadOnlyCollection<
            WorkspaceStaffOnboardingProcessingRestriction> active =
            await restrictions.ListActiveAsync(
                request.Coordinate.RecordId,
                ActiveRestrictionPage,
                cancellationToken).ConfigureAwait(false);
        Result<WorkspaceStaffOnboardingProcessingRestrictionReceiptDto>
            executed = request.Directive switch
            {
                DataRightsRestrictionDirective.Apply =>
                    await dispatcher.SendAsync(
                        new
                            ApplyWorkspaceStaffOnboardingProcessingRestrictionCommand(
                                request.IdempotencyKey,
                                request.CaseId,
                                request.ApprovalRevision,
                                request.Coordinate.RecordId,
                                request.Coordinate.RecordVersion,
                                projection.Revision,
                                request.ExecutingActorId),
                        cancellationToken).ConfigureAwait(false),
                DataRightsRestrictionDirective.Release
                    when active.Count == 1 =>
                    await this.ReleaseAsync(
                        request,
                        projection,
                        active.Single(),
                        cancellationToken).ConfigureAwait(false),
                _ =>
                    Result.Failure<
                        WorkspaceStaffOnboardingProcessingRestrictionReceiptDto>(
                        WorkspaceStaffOnboardingApplicationErrors
                            .RestrictionActiveStateInvalid)
            };

        if (executed.IsFailure)
        {
            return IsBlocked(executed.Error.Code)
                ? DataRightsRestrictionContributionResult.Blocked(
                    executed.Error.Code)
                : DataRightsRestrictionContributionResult.Failed(
                    executed.Error.Code);
        }

        return ToResult(request, executed.Value);
    }

    private static DataRightsRestrictionContributionResult ToResult(
        DataRightsRestrictionContributionRequest request,
        WorkspaceStaffOnboardingProcessingRestrictionReceiptDto receipt)
    {
        if (!Matches(request, receipt))
        {
            return DataRightsRestrictionContributionResult.Failed(
                WorkspaceStaffOnboardingApplicationErrors
                    .RestrictionOwnerProofInvalid.Code);
        }

        return DataRightsRestrictionContributionResult.Completed(
            new DataRightsRestrictionOwnerProof(
                WorkspaceStaffOnboardingProcessingRestrictionContract
                    .CurrentVersion,
                receipt.ReceiptId,
                receipt.RestrictionId,
                receipt.RestrictionVersion,
                receipt.ProjectionRevision,
                receipt.EffectiveRestricted,
                ComputeDigest(receipt),
                receipt.CompletedAtUtc));
    }

    private async Task<Result<
        WorkspaceStaffOnboardingProcessingRestrictionReceiptDto>>
        ReleaseAsync(
            DataRightsRestrictionContributionRequest request,
            WorkspaceStaffOnboardingProcessingRestrictionProjection projection,
            WorkspaceStaffOnboardingProcessingRestriction restriction,
            CancellationToken cancellationToken) =>
        await dispatcher.SendAsync(
            new ReleaseWorkspaceStaffOnboardingProcessingRestrictionCommand(
                request.IdempotencyKey,
                restriction.Id,
                request.CaseId,
                request.ApprovalRevision,
                request.Coordinate.RecordId,
                request.Coordinate.RecordVersion,
                restriction.Version,
                projection.Revision,
                request.ExecutingActorId),
            cancellationToken).ConfigureAwait(false);

    private static bool IsValid(
        DataRightsRestrictionContributionRequest? request,
        DateTimeOffset nowUtc) =>
        request is not null &&
        request.ContractVersion ==
            DataRightsRestrictionContract.CurrentVersion &&
        request.CaseType == DataRightsCaseType.StaffRights &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        request.IdempotencyKey != Guid.Empty &&
        request.PropertyId is null &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        string.Equals(
            request.Coordinate.OwnerKey,
            WorkspacesDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            WorkspacesDataRightsCoordinates.StaffOnboardingRecordType,
            StringComparison.Ordinal) &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0 &&
        request.Directive is DataRightsRestrictionDirective.Apply
            or DataRightsRestrictionDirective.Release &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        request.DeadlineUtc > nowUtc;

    private static bool Matches(
        DataRightsRestrictionContributionRequest request,
        WorkspaceStaffOnboardingProcessingRestrictionReceiptDto receipt)
    {
        WorkspaceStaffOnboardingProcessingRestrictionActionDto expectedAction =
            request.Directive == DataRightsRestrictionDirective.Apply
                ? WorkspaceStaffOnboardingProcessingRestrictionActionDto.Apply
                : WorkspaceStaffOnboardingProcessingRestrictionActionDto
                    .Release;
        return receipt.ReceiptId != Guid.Empty &&
            receipt.RestrictionId != Guid.Empty &&
            receipt.Action == expectedAction &&
            receipt.ApplicationId == request.Coordinate.RecordId &&
            receipt.CaseId == request.CaseId &&
            receipt.ApprovalRevision == request.ApprovalRevision &&
            receipt.SelectedOnboardingVersion ==
                request.Coordinate.RecordVersion &&
            receipt.RestrictionVersion > 0 &&
            receipt.ProjectionRevision > 0 &&
            receipt.EffectiveRestricted ==
                (request.Directive ==
                    DataRightsRestrictionDirective.Apply) &&
            string.Equals(
                receipt.ActorId,
                request.ExecutingActorId.Trim(),
                StringComparison.Ordinal) &&
            receipt.EventId != Guid.Empty &&
            receipt.CompletedAtUtc != default &&
            receipt.CompletedAtUtc <= request.DeadlineUtc;
    }

    private static bool IsBlocked(string code) =>
        code is
            "Workspaces.StaffOnboardingRestrictionActiveStateInvalid"
            or "Workspaces.StaffOnboardingRestrictionProjectionUnavailable"
            or
                "Workspaces.StaffOnboardingRestrictionOnboardingVersionConflict"
            or
                "Workspaces.StaffOnboardingRestrictionProjectionVersionConflict"
            or
                "Workspaces.StaffOnboardingRestrictionAuthorityUnavailable";

    private static string ComputeDigest(
        WorkspaceStaffOnboardingProcessingRestrictionReceiptDto receipt)
    {
        string canonical = string.Join(
            "|",
            receipt.ReceiptId.ToString("D"),
            receipt.RestrictionId.ToString("D"),
            ((int)receipt.Action).ToString(CultureInfo.InvariantCulture),
            receipt.ApplicationId.ToString("D"),
            receipt.CaseId.ToString("D"),
            receipt.ApprovalRevision.ToString(CultureInfo.InvariantCulture),
            receipt.SelectedOnboardingVersion.ToString(
                CultureInfo.InvariantCulture),
            receipt.RestrictionVersion.ToString(CultureInfo.InvariantCulture),
            receipt.ProjectionRevision.ToString(CultureInfo.InvariantCulture),
            receipt.EffectiveRestricted ? "1" : "0",
            receipt.ActorId,
            receipt.EventId.ToString("D"),
            receipt.CompletedAtUtc.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))
            .ToLowerInvariant();
    }
}
