namespace BunkFy.Modules.Staff.Application.Contributors;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Staff.Application.Commands;
using BunkFy.Modules.Staff.Application.Mapping;
using BunkFy.Modules.Staff.Application.Ports;
using BunkFy.Modules.Staff.Contracts;
using BunkFy.Modules.Staff.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class StaffDataRightsRestrictionContributor(
    IRequestDispatcher dispatcher,
    IStaffProcessingRestrictionProjectionRepository projections,
    IStaffProcessingRestrictionRepository restrictions,
    ISystemClock clock)
    : IDataRightsRestrictionContributor
{
    private static readonly PageRequest ActiveRestrictionPage = new(1, 2);

    public string OwnerKey => StaffDataRightsCoordinates.Owner;

    public int ContractVersion => DataRightsRestrictionContract.CurrentVersion;

    public async Task<DataRightsRestrictionContributionResult> ExecuteAsync(
        DataRightsRestrictionContributionRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValid(request, clock.UtcNow))
        {
            return DataRightsRestrictionContributionResult.Failed(
                StaffApplicationErrors.RestrictionRequestInvalid.Code);
        }

        StaffProcessingRestrictionReceipt? replay =
            await restrictions.FindReceiptByIdempotencyKeyAsync(
                request.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return ToResult(request, replay.ToDto());
        }

        StaffProcessingRestrictionProjection? projection =
            await projections.GetAsync(
                request.Coordinate.RecordId,
                cancellationToken).ConfigureAwait(false);
        if (projection is null)
        {
            return DataRightsRestrictionContributionResult.Blocked(
                StaffApplicationErrors.RestrictionProjectionUnavailable.Code);
        }

        IReadOnlyCollection<StaffProcessingRestriction> active =
            await restrictions.ListActiveAsync(
                request.Coordinate.RecordId,
                ActiveRestrictionPage,
                cancellationToken).ConfigureAwait(false);
        Result<StaffProcessingRestrictionReceiptDto> executed =
            request.Directive switch
            {
                DataRightsRestrictionDirective.Apply =>
                    await dispatcher.SendAsync(
                        new ApplyStaffProcessingRestrictionCommand(
                            request.IdempotencyKey,
                            request.CaseId,
                            request.ApprovalRevision,
                            request.Coordinate.RecordId,
                            request.Coordinate.RecordVersion,
                            projection.Revision,
                            request.ExecutingActorId),
                        cancellationToken).ConfigureAwait(false),
                DataRightsRestrictionDirective.Release when active.Count == 1 =>
                    await this.ReleaseAsync(
                        request,
                        projection,
                        active.Single(),
                        cancellationToken).ConfigureAwait(false),
                _ =>
                    Result.Failure<StaffProcessingRestrictionReceiptDto>(
                        StaffApplicationErrors.RestrictionActiveStateInvalid)
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
        StaffProcessingRestrictionReceiptDto receipt)
    {
        if (!Matches(request, receipt))
        {
            return DataRightsRestrictionContributionResult.Failed(
                StaffApplicationErrors.RestrictionOwnerProofInvalid.Code);
        }

        return DataRightsRestrictionContributionResult.Completed(
            new DataRightsRestrictionOwnerProof(
                StaffProcessingRestrictionContract.CurrentVersion,
                receipt.ReceiptId,
                receipt.RestrictionId,
                receipt.RestrictionVersion,
                receipt.ProjectionRevision,
                receipt.EffectiveRestricted,
                ComputeDigest(receipt),
                receipt.CompletedAtUtc));
    }

    private async Task<Result<StaffProcessingRestrictionReceiptDto>> ReleaseAsync(
        DataRightsRestrictionContributionRequest request,
        StaffProcessingRestrictionProjection projection,
        StaffProcessingRestriction restriction,
        CancellationToken cancellationToken) =>
        await dispatcher.SendAsync(
            new ReleaseStaffProcessingRestrictionCommand(
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
        request.ContractVersion == DataRightsRestrictionContract.CurrentVersion &&
        request.CaseType == DataRightsCaseType.StaffRights &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        request.IdempotencyKey != Guid.Empty &&
        request.PropertyId is null &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        string.Equals(
            request.Coordinate.OwnerKey,
            StaffDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            StaffDataRightsCoordinates.StaffMemberRecordType,
            StringComparison.Ordinal) &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0 &&
        request.Directive is DataRightsRestrictionDirective.Apply
            or DataRightsRestrictionDirective.Release &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        request.DeadlineUtc > nowUtc;

    private static bool Matches(
        DataRightsRestrictionContributionRequest request,
        StaffProcessingRestrictionReceiptDto receipt)
    {
        StaffProcessingRestrictionActionDto expectedAction =
            request.Directive == DataRightsRestrictionDirective.Apply
                ? StaffProcessingRestrictionActionDto.Apply
                : StaffProcessingRestrictionActionDto.Release;
        return receipt.ReceiptId != Guid.Empty &&
            receipt.RestrictionId != Guid.Empty &&
            receipt.Action == expectedAction &&
            receipt.StaffMemberId == request.Coordinate.RecordId &&
            receipt.CaseId == request.CaseId &&
            receipt.ApprovalRevision == request.ApprovalRevision &&
            receipt.SelectedStaffVersion == request.Coordinate.RecordVersion &&
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
        code is "Staff.RestrictionActiveStateInvalid"
            or "Staff.RestrictionProjectionUnavailable"
            or "Staff.RestrictionStaffVersionConflict"
            or "Staff.RestrictionProjectionVersionConflict";

    private static string ComputeDigest(
        StaffProcessingRestrictionReceiptDto receipt)
    {
        string canonical = string.Join(
            "|",
            receipt.ReceiptId.ToString("D"),
            receipt.RestrictionId.ToString("D"),
            ((int)receipt.Action).ToString(CultureInfo.InvariantCulture),
            receipt.StaffMemberId.ToString("D"),
            receipt.CaseId.ToString("D"),
            receipt.ApprovalRevision.ToString(CultureInfo.InvariantCulture),
            receipt.SelectedStaffVersion.ToString(CultureInfo.InvariantCulture),
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
