namespace BunkFy.Modules.Guests.Application.Contributors;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.Guests.Application.Commands;
using BunkFy.Modules.Guests.Application.Mapping;
using BunkFy.Modules.Guests.Application.Ports;
using BunkFy.Modules.Guests.Contracts;
using BunkFy.Modules.Guests.Domain.DataRights;
using Gma.Framework.Cqrs;
using Gma.Framework.Pagination;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;

internal sealed class GuestDataRightsRestrictionContributor(
    IRequestDispatcher dispatcher,
    IGuestProcessingRestrictionProjectionRepository projections,
    IGuestProcessingRestrictionRepository restrictions,
    ISystemClock clock)
    : IDataRightsRestrictionContributor
{
    private static readonly PageRequest ActiveRestrictionPage = new(1, 2);

    public string OwnerKey => GuestsDataRightsCoordinates.Owner;

    public int ContractVersion => DataRightsRestrictionContract.CurrentVersion;

    public async Task<DataRightsRestrictionContributionResult> ExecuteAsync(
        DataRightsRestrictionContributionRequest request,
        CancellationToken cancellationToken)
    {
        if (!IsValid(request, clock.UtcNow))
        {
            return DataRightsRestrictionContributionResult.Failed(
                GuestsApplicationErrors.RestrictionRequestInvalid.Code);
        }
        Guid propertyId = request.PropertyId!.Value;

        GuestProcessingRestrictionReceipt? replay =
            await restrictions.FindReceiptByIdempotencyKeyAsync(
                request.IdempotencyKey,
                cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return ToResult(request, replay.ToDto());
        }

        GuestProcessingRestrictionProjection? projection = await projections.GetAsync(
            propertyId,
            request.Coordinate.RecordId,
            cancellationToken).ConfigureAwait(false);
        if (projection is null)
        {
            return DataRightsRestrictionContributionResult.Blocked(
                GuestsApplicationErrors.RestrictionProjectionUnavailable.Code);
        }

        IReadOnlyCollection<GuestProcessingRestriction> active =
            await restrictions.ListActiveAsync(
                propertyId,
                request.Coordinate.RecordId,
                ActiveRestrictionPage,
                cancellationToken).ConfigureAwait(false);
        Result<GuestProcessingRestrictionReceiptDto> executed =
            request.Directive switch
            {
                DataRightsRestrictionDirective.Apply =>
                    await dispatcher.SendAsync(
                        new ApplyGuestProcessingRestrictionCommand(
                            request.IdempotencyKey,
                            propertyId,
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
                    Result.Failure<GuestProcessingRestrictionReceiptDto>(
                        GuestsApplicationErrors.RestrictionActiveStateInvalid)
            };

        if (executed.IsFailure)
        {
            return IsBlocked(executed.Error.Code)
                ? DataRightsRestrictionContributionResult.Blocked(executed.Error.Code)
                : DataRightsRestrictionContributionResult.Failed(executed.Error.Code);
        }

        return ToResult(request, executed.Value);
    }

    private static DataRightsRestrictionContributionResult ToResult(
        DataRightsRestrictionContributionRequest request,
        GuestProcessingRestrictionReceiptDto receipt)
    {
        if (!Matches(request, receipt))
        {
            return DataRightsRestrictionContributionResult.Failed(
                GuestsApplicationErrors.RestrictionOwnerProofInvalid.Code);
        }

        return DataRightsRestrictionContributionResult.Completed(
            new DataRightsRestrictionOwnerProof(
                GuestProcessingRestrictionContract.CurrentVersion,
                receipt.ReceiptId,
                receipt.RestrictionId,
                receipt.RestrictionVersion,
                receipt.ProjectionRevision,
                receipt.EffectiveRestricted,
                ComputeDigest(receipt),
                receipt.CompletedAtUtc));
    }

    private async Task<Result<GuestProcessingRestrictionReceiptDto>> ReleaseAsync(
        DataRightsRestrictionContributionRequest request,
        GuestProcessingRestrictionProjection projection,
        GuestProcessingRestriction restriction,
        CancellationToken cancellationToken) =>
        await dispatcher.SendAsync(
            new ReleaseGuestProcessingRestrictionCommand(
                request.IdempotencyKey,
                request.PropertyId!.Value,
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
        request.CaseType == DataRightsCaseType.GuestRights &&
        !string.IsNullOrWhiteSpace(request.TenantId) &&
        request.IdempotencyKey != Guid.Empty &&
        request.PropertyId is Guid propertyId &&
        propertyId != Guid.Empty &&
        request.CaseId != Guid.Empty &&
        request.ApprovalRevision > 0 &&
        string.Equals(
            request.Coordinate.OwnerKey,
            GuestsDataRightsCoordinates.Owner,
            StringComparison.Ordinal) &&
        string.Equals(
            request.Coordinate.RecordType,
            GuestsDataRightsCoordinates.GuestProfileRecordType,
            StringComparison.Ordinal) &&
        request.Coordinate.RecordId != Guid.Empty &&
        request.Coordinate.RecordVersion > 0 &&
        request.Directive is DataRightsRestrictionDirective.Apply
            or DataRightsRestrictionDirective.Release &&
        !string.IsNullOrWhiteSpace(request.ExecutingActorId) &&
        request.DeadlineUtc > nowUtc;

    private static bool Matches(
        DataRightsRestrictionContributionRequest request,
        GuestProcessingRestrictionReceiptDto receipt)
    {
        GuestProcessingRestrictionActionDto expectedAction =
            request.Directive == DataRightsRestrictionDirective.Apply
                ? GuestProcessingRestrictionActionDto.Apply
                : GuestProcessingRestrictionActionDto.Release;
        return receipt.ReceiptId != Guid.Empty &&
            receipt.RestrictionId != Guid.Empty &&
            receipt.Action == expectedAction &&
            receipt.PropertyId == request.PropertyId!.Value &&
            receipt.GuestId == request.Coordinate.RecordId &&
            receipt.CaseId == request.CaseId &&
            receipt.ApprovalRevision == request.ApprovalRevision &&
            receipt.SelectedGuestVersion == request.Coordinate.RecordVersion &&
            receipt.RestrictionVersion > 0 &&
            receipt.ProjectionRevision > 0 &&
            receipt.EffectiveRestricted ==
                (request.Directive == DataRightsRestrictionDirective.Apply) &&
            string.Equals(
                receipt.ActorId,
                request.ExecutingActorId.Trim(),
                StringComparison.Ordinal) &&
            receipt.EventId != Guid.Empty &&
            receipt.CompletedAtUtc != default &&
            receipt.CompletedAtUtc <= request.DeadlineUtc;
    }

    private static bool IsBlocked(string code) =>
        code is "Guests.RestrictionActiveStateInvalid"
            or "Guests.RestrictionProjectionUnavailable"
            or "Guests.RestrictionGuestVersionConflict"
            or "Guests.RestrictionProjectionVersionConflict";

    private static string ComputeDigest(GuestProcessingRestrictionReceiptDto receipt)
    {
        string canonical = string.Join(
            "|",
            receipt.ReceiptId.ToString("D"),
            receipt.RestrictionId.ToString("D"),
            ((int)receipt.Action).ToString(CultureInfo.InvariantCulture),
            receipt.PropertyId.ToString("D"),
            receipt.GuestId.ToString("D"),
            receipt.CaseId.ToString("D"),
            receipt.ApprovalRevision.ToString(CultureInfo.InvariantCulture),
            receipt.SelectedGuestVersion.ToString(CultureInfo.InvariantCulture),
            receipt.RestrictionVersion.ToString(CultureInfo.InvariantCulture),
            receipt.ProjectionRevision.ToString(CultureInfo.InvariantCulture),
            receipt.EffectiveRestricted ? "1" : "0",
            receipt.ActorId,
            receipt.EventId.ToString("D"),
            receipt.CompletedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
}
