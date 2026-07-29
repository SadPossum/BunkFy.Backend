namespace BunkFy.Modules.DataRights.Application.Tasks;

using BunkFy.Modules.DataRights.Application.Commands;
using BunkFy.Modules.DataRights.Application.Models;
using BunkFy.Modules.DataRights.Contracts;
using BunkFy.Modules.DataRights.Domain.Aggregates;
using Gma.Framework.Cqrs;
using Gma.Framework.Results;
using Gma.Framework.Runtime.Time;
using Gma.Framework.Tasks;
using Gma.Framework.Tasks.Cqrs;

internal sealed class ExecuteDataRightsAnonymisationTaskHandler(
    ITaskCommandDispatcher commandDispatcher,
    IEnumerable<IDataRightsAnonymisationContributor> contributors,
    ISystemClock clock)
    : ITaskHandler<ExecuteDataRightsAnonymisationPayload>
{
    public async Task HandleAsync(
        ExecuteDataRightsAnonymisationPayload payload,
        TaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        Result<DataRightsAnonymisationWorkItemStart> started =
            await commandDispatcher.DispatchAsync<
                BeginDataRightsAnonymisationWorkItemCommand,
                DataRightsAnonymisationWorkItemStart>(
                context,
                new BeginDataRightsAnonymisationWorkItemCommand(
                    payload.WorkItemId,
                    payload.CaseId,
                    DataRightsCaseScope.ForProperty(payload.PropertyId),
                    payload.ApprovalRevision,
                    payload.ExecutionRevision,
                    context.RunId,
                    context.Attempt),
                cancellationToken).ConfigureAwait(false);
        if (started.IsFailure)
        {
            throw Failure(started.Error);
        }

        if (started.Value.DispatchRequired)
        {
            await this.DispatchOwnerAsync(
                payload,
                context,
                started.Value,
                cancellationToken).ConfigureAwait(false);
        }

        Result<Unit> finalized =
            await commandDispatcher.DispatchAsync<
                FinalizeDataRightsAnonymisationLedgerCommand,
                Unit>(
                context,
                new FinalizeDataRightsAnonymisationLedgerCommand(
                    payload.WorkItemId,
                    payload.CaseId,
                    DataRightsCaseScope.ForProperty(payload.PropertyId),
                    payload.ApprovalRevision,
                    payload.ExecutionRevision,
                    context.RunId),
                cancellationToken).ConfigureAwait(false);
        if (finalized.IsFailure)
        {
            throw Failure(finalized.Error);
        }
    }

    private async Task DispatchOwnerAsync(
        ExecuteDataRightsAnonymisationPayload payload,
        TaskExecutionContext context,
        DataRightsAnonymisationWorkItemStart started,
        CancellationToken cancellationToken)
    {
        DataRightsAnonymisationContributionRequest request =
            started.Request ?? throw new InvalidOperationException(
                "DataRights.AnonymisationRequestUnavailable");
        IDataRightsAnonymisationContributor contributor = this.ResolveContributor(request);

        TimeSpan remaining = request.DeadlineUtc - clock.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            throw new TimeoutException("DataRights.AnonymisationOwnerDeadlineExceeded");
        }

        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        deadline.CancelAfter(remaining);

        DataRightsAnonymisationContributionResult result;
        try
        {
            result = await contributor.ExecuteAsync(
                request,
                deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("DataRights.AnonymisationOwnerDeadlineExceeded");
        }

        if (!IsValidResult(request, result))
        {
            throw new InvalidOperationException(
                "DataRights.AnonymisationOwnerResultInvalid");
        }

        Result<Unit> recorded =
            await commandDispatcher.DispatchAsync<
                RecordDataRightsAnonymisationOwnerResultCommand,
                Unit>(
                context,
                new RecordDataRightsAnonymisationOwnerResultCommand(
                    payload.WorkItemId,
                    payload.CaseId,
                    DataRightsCaseScope.ForProperty(payload.PropertyId),
                    payload.ApprovalRevision,
                    payload.ExecutionRevision,
                    context.RunId,
                    context.Attempt,
                    started.WorkItemVersion,
                    result),
                cancellationToken).ConfigureAwait(false);
        if (recorded.IsFailure)
        {
            throw Failure(recorded.Error);
        }
    }

    private IDataRightsAnonymisationContributor ResolveContributor(
        DataRightsAnonymisationContributionRequest request)
    {
        IDataRightsAnonymisationContributor[] matches = contributors
            .Where(contributor =>
                string.Equals(
                    contributor.OwnerKey,
                    request.Coordinate.OwnerKey,
                    StringComparison.Ordinal) &&
                contributor.ContractVersion == request.ContractVersion)
            .Take(2)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                "DataRights.AnonymisationOwnerContributorUnavailable");
    }

    private static bool IsValidResult(
        DataRightsAnonymisationContributionRequest request,
        DataRightsAnonymisationContributionResult? result)
    {
        if (result is null || result.ContractVersion != request.ContractVersion)
        {
            return false;
        }

        if (result.Status == DataRightsAnonymisationContributionStatus.Completed)
        {
            DataRightsAnonymisationOwnerProof? proof = result.OwnerProof;
            return result.OutcomeCode is null &&
                proof is not null &&
                proof.ReceiptContractVersion > 0 &&
                proof.ReceiptId != Guid.Empty &&
                proof.ResultingRecordVersion > request.Coordinate.RecordVersion &&
                IsCode(proof.DispositionCode, DataRightsExecutionWorkItem.OwnerCodeMaxLength) &&
                IsCode(proof.ReasonCode, DataRightsExecutionWorkItem.OwnerCodeMaxLength) &&
                IsSha256(proof.ReceiptSha256) &&
                proof.CompletedAtUtc != default &&
                proof.CompletedAtUtc <= request.DeadlineUtc;
        }

        return result.Status is DataRightsAnonymisationContributionStatus.Blocked
                or DataRightsAnonymisationContributionStatus.Failed &&
            result.OwnerProof is null &&
            IsCode(result.OutcomeCode, DataRightsAnonymisationContract.CodeMaxLength);
    }

    private static bool IsCode(string? value, int maxLength)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length is > 0 && normalized.Length <= maxLength;
    }

    private static bool IsSha256(string? value)
    {
        string normalized = value?.Trim() ?? string.Empty;
        return normalized.Length == DataRightsAnonymisationContract.Sha256Length &&
            normalized.All(Uri.IsHexDigit);
    }

    private static InvalidOperationException Failure(Error error) =>
        new($"{error.Code}: {error.Message}");
}
