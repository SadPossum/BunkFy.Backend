namespace BunkFy.Modules.DataRights.Domain.Aggregates;

using BunkFy.Modules.DataRights.Domain.Errors;
using BunkFy.Modules.DataRights.Domain.Models;
using Gma.Framework.Results;

public sealed partial class TenantTerminationProcess
{
    public Result ConfirmVerification(
        long verificationOperationRevision,
        Guid terminalReceiptId,
        long terminalReceiptVersion,
        string ownerProofSetSha256,
        long expectedVersion,
        string actorId,
        DateTimeOffset nowUtc)
    {
        string normalizedActor = NormalizeActor(actorId);
        if (this.IsExactVerificationConfirmation(
                verificationOperationRevision,
                terminalReceiptId,
                terminalReceiptVersion,
                ownerProofSetSha256,
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

        if (this.Phase != TenantTerminationProcessPhase.Verify ||
            this.Status != TenantTerminationProcessStatus.Running ||
            verificationOperationRevision != this.OperationRevision ||
            this.DestroyCompletedOperationRevision is not long destroyRevision ||
            destroyRevision <= this.ApprovalRevision ||
            destroyRevision >= verificationOperationRevision ||
            !this.DestroyedAtUtc.HasValue ||
            terminalReceiptId == Guid.Empty ||
            terminalReceiptVersion <= 0 ||
            !IsSha256(ownerProofSetSha256) ||
            this.VerificationConfirmedOperationRevision.HasValue)
        {
            return Result.Failure(
                DataRightsDomainErrors
                    .TenantTerminationVerificationConfirmationInvalid);
        }

        this.VerificationConfirmationRevision++;
        this.VerificationConfirmedOperationRevision =
            verificationOperationRevision;
        this.TerminalReceiptId = terminalReceiptId;
        this.TerminalReceiptVersion = terminalReceiptVersion;
        this.VerificationOwnerProofSetSha256 = ownerProofSetSha256;
        this.VerificationConfirmedBy = normalizedActor;
        this.VerificationConfirmedAtUtc = nowUtc;
        this.CompleteChange(normalizedActor, nowUtc);
        return Result.Success();
    }

    public bool HasCurrentVerificationConfirmation() =>
        this.VerificationConfirmationRevision > 0 &&
        this.VerificationConfirmedOperationRevision == this.OperationRevision &&
        this.TerminalReceiptId is Guid receiptId &&
        receiptId != Guid.Empty &&
        this.TerminalReceiptVersion is > 0 &&
        IsSha256(this.VerificationOwnerProofSetSha256) &&
        NormalizeActor(this.VerificationConfirmedBy).Length > 0 &&
        this.VerificationConfirmedAtUtc.HasValue;

    private bool IsExactVerificationConfirmation(
        long verificationOperationRevision,
        Guid terminalReceiptId,
        long terminalReceiptVersion,
        string ownerProofSetSha256,
        string actorId,
        DateTimeOffset confirmedAtUtc) =>
        this.Phase == TenantTerminationProcessPhase.Verify &&
        this.Status == TenantTerminationProcessStatus.Running &&
        this.VerificationConfirmedOperationRevision ==
            verificationOperationRevision &&
        this.TerminalReceiptId == terminalReceiptId &&
        this.TerminalReceiptVersion == terminalReceiptVersion &&
        string.Equals(
            this.VerificationOwnerProofSetSha256,
            ownerProofSetSha256,
            StringComparison.Ordinal) &&
        string.Equals(
            this.VerificationConfirmedBy,
            actorId,
            StringComparison.Ordinal) &&
        this.VerificationConfirmedAtUtc == confirmedAtUtc;

    private void ClearVerificationConfirmation()
    {
        this.VerificationConfirmedOperationRevision = null;
        this.TerminalReceiptId = null;
        this.TerminalReceiptVersion = null;
        this.VerificationOwnerProofSetSha256 = null;
        this.VerificationConfirmedBy = null;
        this.VerificationConfirmedAtUtc = null;
    }
}
