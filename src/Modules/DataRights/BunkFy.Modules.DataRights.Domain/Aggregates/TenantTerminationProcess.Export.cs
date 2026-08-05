namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class TenantTerminationProcess
{
    public Result ConfirmExport(
        long exportOperationRevision,
        Guid artifactId,
        long artifactVersion,
        string frozenRevisionSha256,
        string fragmentSetSha256,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        string normalizedActor = NormalizeActor(actorId);
        if (this.IsExactExportConfirmation(
                exportOperationRevision,
                artifactId,
                artifactVersion,
                frozenRevisionSha256,
                fragmentSetSha256,
                normalizedActor,
                nowUtc))
        {
            return Result.Success();
        }

        Result ready = this.ValidateChange(expectedVersion, actorId, nowUtc);
        if (ready.IsFailure)
        {
            return ready;
        }

        if (!this.ExportRequested ||
            this.Phase != TenantTerminationProcessPhase.Export ||
            this.Status != TenantTerminationProcessStatus.Running ||
            exportOperationRevision != this.OperationRevision ||
            artifactId == Guid.Empty ||
            artifactVersion <= 0 ||
            !IsSha256(frozenRevisionSha256) ||
            !IsSha256(fragmentSetSha256) ||
            !string.Equals(
                this.FrozenRevisionSha256,
                frozenRevisionSha256,
                StringComparison.Ordinal) ||
            this.ExportConfirmedOperationRevision.HasValue)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationExportConfirmationInvalid);
        }

        this.ExportConfirmationRevision++;
        this.ExportConfirmedOperationRevision = exportOperationRevision;
        this.ExportArtifactId = artifactId;
        this.ExportArtifactVersion = artifactVersion;
        this.ExportFrozenRevisionSha256 = frozenRevisionSha256;
        this.ExportFragmentSetSha256 = fragmentSetSha256;
        this.ExportConfirmedBy = normalizedActor;
        this.ExportConfirmedAtUtc = nowUtc;
        this.CompleteChange(normalizedActor, nowUtc);
        return Result.Success();
    }

    public bool HasCurrentExportConfirmation() =>
        this.ExportConfirmationRevision > 0 &&
        this.ExportConfirmedOperationRevision == this.OperationRevision &&
        this.ExportArtifactId is Guid artifactId &&
        artifactId != Guid.Empty &&
        this.ExportArtifactVersion is > 0 &&
        IsSha256(this.ExportFrozenRevisionSha256) &&
        string.Equals(
            this.ExportFrozenRevisionSha256,
            this.FrozenRevisionSha256,
            StringComparison.Ordinal) &&
        IsSha256(this.ExportFragmentSetSha256) &&
        NormalizeActor(this.ExportConfirmedBy).Length > 0 &&
        this.ExportConfirmedAtUtc.HasValue;

    private bool IsExactExportConfirmation(
        long exportOperationRevision,
        Guid artifactId,
        long artifactVersion,
        string frozenRevisionSha256,
        string fragmentSetSha256,
        string actorId,
        DateTimeOffset confirmedAtUtc) =>
        this.Phase == TenantTerminationProcessPhase.Export &&
        this.Status == TenantTerminationProcessStatus.Running &&
        this.ExportConfirmedOperationRevision == exportOperationRevision &&
        this.ExportArtifactId == artifactId &&
        this.ExportArtifactVersion == artifactVersion &&
        string.Equals(
            this.ExportFrozenRevisionSha256,
            frozenRevisionSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            this.ExportFragmentSetSha256,
            fragmentSetSha256,
            StringComparison.Ordinal) &&
        string.Equals(this.ExportConfirmedBy, actorId, StringComparison.Ordinal) &&
        this.ExportConfirmedAtUtc == confirmedAtUtc;

    private void ClearExportConfirmation()
    {
        this.ExportConfirmedOperationRevision = null;
        this.ExportArtifactId = null;
        this.ExportArtifactVersion = null;
        this.ExportFrozenRevisionSha256 = null;
        this.ExportFragmentSetSha256 = null;
        this.ExportConfirmedBy = null;
        this.ExportConfirmedAtUtc = null;
    }
}
